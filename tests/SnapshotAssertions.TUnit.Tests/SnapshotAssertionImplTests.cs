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
                acceptModeOverride: true, baseDirectory: baseDirectory);

            var expectedRuntimeDir = Path.GetFullPath(Path.Combine(baseDirectory, "Snapshots"));
            await Assert.That(resolved).IsEqualTo(expectedRuntimeDir);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
