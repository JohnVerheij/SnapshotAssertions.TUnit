using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions.TUnit;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the one branch of <see cref="SnapshotAssertionImpl.ResolvePaths"/> that cannot be
/// reached from a normal <c>MatchesSnapshot</c> call inside a <c>[Test]</c> method: the
/// defensive throw when no explicit name or path is supplied AND there is no active TUnit
/// test context. <c>TestContext.Current</c> is an <c>AsyncLocal</c> set by the test
/// framework, so any call originating from a test method will see a non-null context. To
/// exercise the null branch the call must run on a thread whose <c>ExecutionContext</c>
/// did not flow from the parent test.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotAssertionImplTests
{
    [Test]
    public async Task ResolvePaths_NoTestContextNoExplicitNameOrPath_ThrowsInvalidOperation(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        InvalidOperationException? captured = null;

        // SuppressFlow stops the parent's ExecutionContext (and therefore the AsyncLocal
        // backing TestContext.Current) from being copied into the new thread. The Thread
        // is started inside the suppressed block; Thread.Join blocks the caller until the
        // captured exception is observed.
        using (ExecutionContext.SuppressFlow())
        {
            var thread = new Thread(() =>
            {
                try
                {
                    SnapshotAssertionImpl.ResolvePaths(null, null);
                }
                catch (InvalidOperationException ex)
                {
                    captured = ex;
                }
            });
            thread.Start();
            thread.Join();
        }

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).Contains("MatchesSnapshot()");
        await Assert.That(captured.Message).Contains("active TUnit test context");
    }

    /// <summary>
    /// Read path: with accept-mode off, a name-derived snapshot resolves against the runtime
    /// copy under <c>bin/</c> (<see cref="AppContext.BaseDirectory"/>).
    /// </summary>
    [Test]
    public async Task ResolvePaths_AcceptModeOff_ResolvesAgainstRuntimeBinDirectory(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var paths = SnapshotAssertionImpl.ResolvePaths(explicitPath: null, explicitName: "ReadPath", acceptModeOverride: false);

        var expectedRuntimeDir = Path.Combine(AppContext.BaseDirectory, "Snapshots");
        await Assert.That(Path.GetDirectoryName(paths.ExpectedFilePath))
            .IsEqualTo(Path.GetFullPath(expectedRuntimeDir));
    }

    /// <summary>
    /// Accept path: with accept-mode on, the same name-derived snapshot resolves against the
    /// <em>source-tree</em> <c>Snapshots/</c> folder beside the test project file, not the
    /// <c>bin/</c> copy. This is the fix that makes an accepted baseline land where it is
    /// committed and read back, instead of being discarded with the build output.
    /// </summary>
    [Test]
    public async Task ResolvePaths_AcceptModeOn_ResolvesAgainstSourceSnapshotsDirectory(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var paths = SnapshotAssertionImpl.ResolvePaths(explicitPath: null, explicitName: "AcceptPath", acceptModeOverride: true);

        var resolvedDir = Path.GetDirectoryName(paths.ExpectedFilePath)!;

        // The resolved accept target is the source project's Snapshots folder: it ends in
        // "Snapshots" and, crucially, is NOT under the build-output bin directory.
        await Assert.That(Path.GetFileName(resolvedDir)).IsEqualTo("Snapshots");
        await Assert.That(resolvedDir).DoesNotContain($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

        // It also differs from the read-path (bin) directory the same call produces with
        // accept-mode off, proving the redirect actually changed the target.
        var readPathDir = Path.GetDirectoryName(
            SnapshotAssertionImpl.ResolvePaths(null, "AcceptPath", acceptModeOverride: false).ExpectedFilePath)!;
        await Assert.That(resolvedDir).IsNotEqualTo(readPathDir);
    }

    /// <summary>
    /// Accept-mode fallback: when no ancestor project file exists above the base directory (for
    /// example a single-file publish), the source-tree lookup returns null and resolution falls
    /// back to the runtime <c>Snapshots/</c> directory beside the base directory rather than
    /// fabricating a bogus source path.
    /// </summary>
    [Test]
    public async Task ResolveSnapshotsDirectory_AcceptModeOn_NoSourceTree_FallsBackToRuntimeDirectory(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // A deep temp directory with no *.csproj anywhere above it (system temp dirs never carry
        // one), so the source-tree lookup finds no ancestor project and returns null.
        var root = Path.Combine(Path.GetTempPath(), "snapshot-impl-fallback-" + Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(root, "a", "b", "c");
        Directory.CreateDirectory(baseDirectory);
        try
        {
            var resolved = SnapshotAssertionImpl.ResolveSnapshotsDirectory(
                acceptModeOverride: true, baseDirectory: baseDirectory, projectDirectory: null);

            var expectedRuntimeDir = Path.GetFullPath(Path.Combine(baseDirectory, "Snapshots"));
            await Assert.That(resolved).IsEqualTo(expectedRuntimeDir);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Accept mode with a known project directory writes into that project's <c>Snapshots/</c> folder even
    /// when the base directory (the build output) is outside it and has no project file above it, as with
    /// <c>UseArtifactsOutput</c> or <c>dotnet test -o</c>.
    /// </summary>
    [Test]
    public async Task ResolveSnapshotsDirectory_AcceptModeOn_KnownProjectDirectory_UsesProjectSnapshots(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var root = CreateTempDirectory();
        try
        {
            var project = Path.Combine(root, "proj");
            var output = Path.Combine(root, "out", "bin");
            Directory.CreateDirectory(project);
            Directory.CreateDirectory(output);

            var resolved = SnapshotAssertionImpl.ResolveSnapshotsDirectory(true, output, project);

            await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(project, "Snapshots")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Without accept mode the project directory is ignored: the read path stays the runtime copy.
    /// </summary>
    [Test]
    public async Task ResolveSnapshotsDirectory_AcceptModeOff_KnownProjectDirectory_UsesRuntimeDirectory(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var root = CreateTempDirectory();
        try
        {
            var project = Path.Combine(root, "proj");
            var output = Path.Combine(root, "out");
            Directory.CreateDirectory(project);
            Directory.CreateDirectory(output);

            var resolved = SnapshotAssertionImpl.ResolveSnapshotsDirectory(false, output, project);

            await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(output, "Snapshots")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A recorded project directory that does not exist (a build from another machine, a moved checkout)
    /// is treated as unknown: accept mode falls back to the walk-up, which finds an F# project file.
    /// </summary>
    [Test]
    public async Task ResolveSnapshotsDirectory_AcceptModeOn_MissingProjectDirectory_FallsBackToWalkUp(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var root = CreateTempDirectory();
        try
        {
            var project = Path.Combine(root, "fsproj-dir");
            var output = Path.Combine(project, "bin", "Debug");
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(project, "Tests.fsproj"), "<Project/>", ct).ConfigureAwait(false);
            var missing = Path.Combine(root, "does-not-exist");

            var resolved = SnapshotAssertionImpl.ResolveSnapshotsDirectory(true, output, missing);

            await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(project, "Snapshots")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A default-named baseline with a known project folder reports the project-side path, derived the same
    /// way as the build-output path (class, method and argument hash).
    /// </summary>
    [Test]
    public async Task Resolve_DefaultName_KnownProject_ReportsProjectBaseline(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var root = CreateTempDirectory();
        try
        {
            var resolved = SnapshotAssertionImpl.Resolve(null, null, false, root, root);

            var expectedFileName = Path.GetFileName(resolved.Paths.ExpectedFilePath);
            await Assert.That(resolved.ProjectBaselinePath)
                .IsEqualTo(Path.GetFullPath(Path.Combine(root, "Snapshots", expectedFileName)));
            await Assert.That(expectedFileName).StartsWith(nameof(SnapshotAssertionImplTests) + ".");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A <c>.WithName(...)</c> baseline with a known project folder reports the project-side path.</summary>
    [Test]
    public async Task Resolve_ExplicitName_KnownProject_ReportsProjectBaseline(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var root = CreateTempDirectory();
        try
        {
            var output = Path.Combine(root, "out");
            Directory.CreateDirectory(output);
            var project = Path.Combine(root, "proj");
            Directory.CreateDirectory(project);

            var resolved = SnapshotAssertionImpl.Resolve(null, "named", false, output, project);

            await Assert.That(resolved.ProjectBaselinePath)
                .IsEqualTo(Path.GetFullPath(Path.Combine(project, "Snapshots", "named.expected.txt")));
            await Assert.That(resolved.Paths.ExpectedFilePath)
                .IsEqualTo(Path.GetFullPath(Path.Combine(output, "Snapshots", "named.expected.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>An explicit file path (<c>AtPath</c> / <c>MatchesSnapshotFile</c>) has no project baseline line.</summary>
    [Test]
    public async Task Resolve_ExplicitPath_KnownProject_HasNoProjectBaseline(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var root = CreateTempDirectory();
        try
        {
            var explicitPath = Path.Combine(root, "golden.expected.txt");

            var resolved = SnapshotAssertionImpl.Resolve(explicitPath, null, false, root, root);

            await Assert.That(resolved.ProjectBaselinePath).IsNull();
            await Assert.That(resolved.Paths.ExpectedFilePath).IsEqualTo(Path.GetFullPath(explicitPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>With an unknown (null) or non-existent project folder there is no project baseline line.</summary>
    [Test]
    [Arguments(null)]
    [Arguments("does-not-exist-project-dir")]
    public async Task Resolve_UnknownProject_HasNoProjectBaseline(string? projectDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var root = CreateTempDirectory();
        try
        {
            var project = projectDirectory is null ? null : Path.Combine(root, projectDirectory);

            var resolved = SnapshotAssertionImpl.Resolve(null, "named", false, root, project);

            await Assert.That(resolved.ProjectBaselinePath).IsNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The failure message is the core description followed by exactly one <c>Project baseline:</c> line.
    /// </summary>
    [Test]
    public async Task EvaluateAsync_Mismatch_WithProjectBaseline_AppendsOneLineAfterCoreMessage(CancellationToken ct)
    {
        var (root, paths) = await CreateMismatchAsync(ct).ConfigureAwait(false);
        try
        {
            var core = await SnapshotAssertionImpl.EvaluateAsync(
                "actual\n", null, SnapshotOptions.Default, paths, null, ct).ConfigureAwait(false);
            var projectPath = Path.Combine(root, "proj", "Snapshots", "x.expected.txt");

            var result = await SnapshotAssertionImpl.EvaluateAsync(
                "actual\n", null, SnapshotOptions.Default, paths, projectPath, ct).ConfigureAwait(false);

            await Assert.That(core.IsPassed).IsFalse();
            await Assert.That(core.Message).DoesNotContain("Project baseline:");
            await Assert.That(result.Message).IsEqualTo(core.Message + "Project baseline: " + projectPath + Environment.NewLine);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A passing evaluation produces no failure, whatever the project baseline.</summary>
    [Test]
    public async Task EvaluateAsync_Match_WithProjectBaseline_Passes(CancellationToken ct)
    {
        var (root, paths) = await CreateMismatchAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await SnapshotAssertionImpl.EvaluateAsync(
                "expected\n", null, SnapshotOptions.Default, paths, "ignored", ct).ConfigureAwait(false);

            await Assert.That(result.IsPassed).IsTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(string Root, SnapshotPaths Paths)> CreateMismatchAsync(CancellationToken ct)
    {
        var root = CreateTempDirectory();
        var expected = Path.Combine(root, "x.expected.txt");
        await File.WriteAllTextAsync(expected, "expected\n", ct).ConfigureAwait(false);
        return (root, SnapshotFileResolver.ResolveByFile(expected));
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "snapshot-impl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
