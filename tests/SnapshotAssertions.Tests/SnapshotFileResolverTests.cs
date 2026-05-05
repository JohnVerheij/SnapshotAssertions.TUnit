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
}
