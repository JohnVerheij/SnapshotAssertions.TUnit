using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the path-construction logic in <see cref="SnapshotFileResolver"/>. Resolves are pure
/// (no IO) and produce absolute paths regardless of input.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotFileResolverTests
{
    private static readonly object?[] ArgsOneTwo = [new[] { 1, 2 }];
    private static readonly object?[] ArgsOneTwoCopy = [new[] { 1, 2 }];
    private static readonly object?[] ArgsThreeFour = [new[] { 3, 4 }];
    private static readonly object?[] ArgsNestedA = [new[] { new[] { 1 }, new[] { 2 } }];
    private static readonly object?[] ArgsNestedB = [new[] { new[] { 1 }, new[] { 9 } }];
    private static readonly object?[] ArgsMixed = [1, "two", null];
    private static readonly object?[] ArgsMixedCopy = [1, "two", null];
    private static readonly object?[] ArgsMixedOther = [2, "two", null];
    private static readonly object?[] ArgsCustom = [new CustomArg()];
    private static readonly object?[] ArgsRawToken = ["custom-token"];

    /// <summary>ResolveByName builds <c>{dir}/{name}.expected.txt</c> and the actual sibling.</summary>
    [Test]
    public async Task ResolveByName_ReturnsExpectedAndActualSibling(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var paths = SnapshotFileResolver.ResolveByName(dir, "MyName");

        await Assert.That(paths.ExpectedFilePath).EndsWith("MyName.expected.txt");
        await Assert.That(paths.ActualFilePath).EndsWith("MyName.actual.txt");
        await Assert.That(Path.GetDirectoryName(paths.ExpectedFilePath))
            .IsEqualTo(Path.GetDirectoryName(paths.ActualFilePath));
    }

    /// <summary>ResolveByTest joins class.method as the base name.</summary>
    [Test]
    public async Task ResolveByTest_JoinsClassAndMethod(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var paths = SnapshotFileResolver.ResolveByTest(dir, "MyClass", "MyMethod");

        await Assert.That(paths.ExpectedFilePath).EndsWith("MyClass.MyMethod.expected.txt");
        await Assert.That(paths.ActualFilePath).EndsWith("MyClass.MyMethod.actual.txt");
    }

    /// <summary>ResolveByFile preserves the explicit path and siblings the actual file.</summary>
    [Test]
    public async Task ResolveByFile_PreservesExpectedPath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var explicitPath = Path.Combine(dir, "Custom.expected.txt");
        var paths = SnapshotFileResolver.ResolveByFile(explicitPath);

        await Assert.That(paths.ExpectedFilePath).IsEqualTo(Path.GetFullPath(explicitPath));
        await Assert.That(paths.ActualFilePath).IsEqualTo(Path.GetFullPath(Path.Combine(dir, "Custom.actual.txt")));
    }

    /// <summary>ResolveByFile tolerates non-canonical extensions by inserting <c>.actual</c>
    /// before the extension on the actual file.</summary>
    [Test]
    public async Task ResolveByFile_NonCanonicalExtension_SiblingsActualPath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var explicitPath = Path.Combine(dir, "Custom.json");
        var paths = SnapshotFileResolver.ResolveByFile(explicitPath);

        await Assert.That(paths.ActualFilePath).EndsWith("Custom.actual.json");
    }

    /// <summary>Path-traversal characters in the snapshot name throw.</summary>
    [Test]
    public void ResolveByName_PathSeparator_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("/tmp", "../escape"));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("/tmp", "sub/path"));
    }

    /// <summary>Empty / whitespace name throws.</summary>
    [Test]
    public void ResolveByName_EmptyName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("/tmp", ""));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("/tmp", "   "));
    }

    /// <summary>Empty / whitespace snapshots-directory throws. Without this check, an empty
    /// directory string would Path.Combine into the bare snapshot name and Path.GetFullPath
    /// would resolve it against the process working directory: almost never what the
    /// caller intended.</summary>
    [Test]
    public void ResolveByName_EmptyDirectory_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName(string.Empty, "MyName"));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("   ", "MyName"));
    }

    /// <summary>GetDefaultSnapshotsDirectory appends the conventional <c>Snapshots</c>
    /// folder to the supplied base directory.</summary>
    [Test]
    public async Task GetDefaultSnapshotsDirectory_AppendsSnapshotsFolder(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var baseDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "base"));
        var snaps = SnapshotFileResolver.GetDefaultSnapshotsDirectory(baseDir);

        await Assert.That(snaps).IsEqualTo(Path.GetFullPath(Path.Combine(baseDir, "Snapshots")));
    }

    /// <summary>Empty / whitespace test-class name throws.</summary>
    [Test]
    public void ResolveByTest_EmptyTestClassName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest("/tmp", string.Empty, "M"));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest("/tmp", "   ", "M"));
    }

    /// <summary>Empty / whitespace test-method name throws.</summary>
    [Test]
    public void ResolveByTest_EmptyTestMethodName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest("/tmp", "C", string.Empty));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest("/tmp", "C", "   "));
    }

    /// <summary>Distinct collection arguments resolve to distinct snapshot files. A collection has no
    /// value-based ToString, so before recursive stringification every array argument of a type
    /// collapsed onto one file, colliding the parameterized variants.</summary>
    [Test]
    public async Task ResolveByTest_DistinctCollectionArgs_ProduceDistinctFiles(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsOneTwo);
        var second = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsThreeFour);
        await Assert.That(first.ExpectedFilePath).IsNotEqualTo(second.ExpectedFilePath);
    }

    /// <summary>Equal collection arguments (distinct instances, same values) resolve to the same file,
    /// so a re-run of the same parameterized variant reads back its own baseline.</summary>
    [Test]
    public async Task ResolveByTest_EqualCollectionArgs_ProduceSameFile(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsOneTwo);
        var second = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsOneTwoCopy);
        await Assert.That(first.ExpectedFilePath).IsEqualTo(second.ExpectedFilePath);
    }

    /// <summary>Nested collections are expanded recursively, so a difference in a nested element still
    /// produces a distinct file.</summary>
    [Test]
    public async Task ResolveByTest_NestedCollectionArgs_AreDistinguished(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsNestedA);
        var second = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsNestedB);
        await Assert.That(first.ExpectedFilePath).IsNotEqualTo(second.ExpectedFilePath);
    }

    /// <summary>A mix of scalar, string, and null arguments hashes deterministically and distinctly:
    /// the per-argument separator, the null sentinel, the verbatim string, and the invariant-culture
    /// formatting of the scalar all participate.</summary>
    [Test]
    public async Task ResolveByTest_MixedScalarStringNullArgs_DistinctAndStable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsMixed);
        var firstAgain = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsMixedCopy);
        var second = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsMixedOther);

        await Assert.That(first.ExpectedFilePath).IsEqualTo(firstAgain.ExpectedFilePath);
        await Assert.That(first.ExpectedFilePath).IsNotEqualTo(second.ExpectedFilePath);
    }

    /// <summary>A non-formattable, non-enumerable argument falls back to its own <c>ToString</c>.</summary>
    [Test]
    public async Task ResolveByTest_CustomObjectArg_UsesItsToString(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var withCustom = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsCustom);
        var withRaw = SnapshotFileResolver.ResolveByTest("/tmp", "C", "M", ArgsRawToken);

        // The custom type's ToString returns "custom-token", so it hashes the same as that string arg.
        await Assert.That(withCustom.ExpectedFilePath).IsEqualTo(withRaw.ExpectedFilePath);
    }

    private sealed class CustomArg
    {
        public override string ToString() => "custom-token";
    }

    /// <summary>TryResolveSourceSnapshotsDirectory walks up from a bin-like runtime directory to
    /// the ancestor holding the project file, then targets that project's <c>Snapshots</c>
    /// folder: the same directory the build's include glob is relative to (where an accepted
    /// baseline must land to be committed and read back).</summary>
    [Test]
    public async Task TryResolveSourceSnapshotsDirectory_FindsProjectAncestorSnapshots(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = CreateTempDirectory();
        try
        {
            // Lay out <root>/Proj/MyProj.csproj and a runtime-style <root>/Proj/bin/Release/net10.0.
            var projectDir = Path.Combine(root, "Proj");
            Directory.CreateDirectory(projectDir);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "MyProj.csproj"), "<Project/>", cancellationToken).ConfigureAwait(false);
            var binDir = Path.Combine(projectDir, "bin", "Release", "net10.0");
            Directory.CreateDirectory(binDir);

            var resolved = SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(binDir);

            await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(projectDir, "Snapshots")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>TryResolveSourceSnapshotsDirectory returns null when no ancestor project file
    /// exists, so accept-mode can fall back to the runtime directory rather than fabricating a
    /// bogus source path.</summary>
    [Test]
    public async Task TryResolveSourceSnapshotsDirectory_NoProjectAncestor_ReturnsNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = CreateTempDirectory();
        try
        {
            // A deep directory tree with no *.csproj anywhere above the start directory.
            var deep = Path.Combine(root, "a", "b", "c");
            Directory.CreateDirectory(deep);

            var resolved = SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(deep);

            await Assert.That(resolved).IsNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        // A unique temp root with no *.csproj anywhere above it (system temp dirs never carry
        // one), so the no-ancestor test sees a clean null and the found-ancestor test only sees
        // the project file this test plants.
        var dir = Path.Combine(Path.GetTempPath(), "snapshot-source-resolver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
