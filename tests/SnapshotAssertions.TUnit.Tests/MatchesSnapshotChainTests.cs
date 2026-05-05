using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the chain methods on <see cref="SnapshotAssertion"/>: <c>WithName</c>,
/// <c>WithOptions</c>, and the path-resolution flow that uses
/// <see cref="SnapshotFileResolver.GetDefaultSnapshotsDirectory"/>. The
/// <see cref="MatchesSnapshotTests"/> file covers <c>AtPath</c> + <c>MatchesSnapshotFile</c>;
/// this file fills the remaining chain-method gaps.
/// </summary>
[Category("Smoke")]
[Timeout(15_000)]
internal sealed class MatchesSnapshotChainTests
{
    /// <summary><c>WithName</c> resolves the file under
    /// <see cref="AppContext.BaseDirectory"/>/Snapshots/{name}.expected.txt.</summary>
    [Test]
    public async Task WithName_ResolvesFromDefaultSnapshotsDirectory(CancellationToken cancellationToken)
    {
        var name = "WithNameTest_" + Guid.NewGuid().ToString("N");
        await WithExpectedFileAsync(name, "hi-by-name\n", async () =>
        {
            await Assert.That("hi-by-name\n").MatchesSnapshot().WithName(name);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>MatchesSnapshot(name)</c> shorthand is equivalent to the
    /// <c>WithName</c> chain.</summary>
    [Test]
    public async Task ShorthandWithName_Matches(CancellationToken cancellationToken)
    {
        var name = "ShorthandName_" + Guid.NewGuid().ToString("N");
        await WithExpectedFileAsync(name, "shorthand-name\n", async () =>
        {
            await Assert.That("shorthand-name\n").MatchesSnapshot(name);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>MatchesSnapshot(name, options)</c> shorthand applies both the name
    /// override and the options.</summary>
    [Test]
    public async Task ShorthandWithNameAndOptions_NormalizedLineEndings_MatchesAcrossLineBreaks(CancellationToken cancellationToken)
    {
        var name = "ShorthandNameOptions_" + Guid.NewGuid().ToString("N");
        await WithExpectedFileAsync(name, "a\r\nb\r\n", async () =>
        {
            await Assert.That("a\nb\n").MatchesSnapshot(name, SnapshotOptions.NormalizedLineEndings);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>MatchesSnapshot(options)</c> shorthand applies the options without
    /// overriding the name (so the test-context-derived name is used).</summary>
    [Test]
    public async Task ShorthandWithOptionsOnly_UsesTestContextDerivedName(CancellationToken cancellationToken)
    {
        var className = nameof(MatchesSnapshotChainTests);
        var methodName = nameof(ShorthandWithOptionsOnly_UsesTestContextDerivedName);
        await WithExpectedFileAsync($"{className}.{methodName}", "ctx-name\n", async () =>
        {
            await Assert.That("ctx-name\n").MatchesSnapshot(SnapshotOptions.Default);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>MatchesSnapshotFile(path, options)</c> applies both the path override and
    /// the options.</summary>
    [Test]
    public async Task ShorthandFileWithOptions_NormalizedLineEndings_MatchesAcrossLineBreaks(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "diff.expected.txt");
        await File.WriteAllTextAsync(expected, "a\r\nb\r\n", cancellationToken).ConfigureAwait(false);

        await Assert.That("a\nb\n").MatchesSnapshotFile(expected, SnapshotOptions.NormalizedLineEndings);
    }

    /// <summary>The default <c>MatchesSnapshot()</c> entry uses the test-context-derived
    /// name (<c>{TestClassName}.{TestMethodName}</c>) under the default
    /// <c>Snapshots/</c> directory.</summary>
    [Test]
    public async Task DefaultPath_UsesTestContextDerivedName(CancellationToken cancellationToken)
    {
        var className = nameof(MatchesSnapshotChainTests);
        var methodName = nameof(DefaultPath_UsesTestContextDerivedName);
        await WithExpectedFileAsync($"{className}.{methodName}", "default-ctx\n", async () =>
        {
            await Assert.That("default-ctx\n").MatchesSnapshot();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>For parameterized tests, the resolver appends an args-hash to the snapshot
    /// name so each variant gets a distinct baseline file. This test exercises that path
    /// directly via <see cref="SnapshotFileResolver.ResolveByTest"/>.</summary>
    [Test]
    public async Task ResolveByTest_WithArgs_AppendsHashSuffix(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = CreateTempDirectory();
        var paths = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 1, "foo" });

        await Assert.That(paths.ExpectedFilePath).EndsWith(".expected.txt");
        // The base "C.M" is followed by a dot then 8 hex chars before .expected.txt.
        var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(paths.ExpectedFilePath));
        await Assert.That(fileName).StartsWith("C.M.");
    }

    /// <summary>Args-hash is stable across calls with the same arguments.</summary>
    [Test]
    public async Task ResolveByTest_SameArgs_ProduceSameHash(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = CreateTempDirectory();
        var pathsA = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 42, "hello", null });
        var pathsB = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 42, "hello", null });

        await Assert.That(pathsA.ExpectedFilePath).IsEqualTo(pathsB.ExpectedFilePath);
    }

    /// <summary>Different args produce different hashes.</summary>
    [Test]
    public async Task ResolveByTest_DifferentArgs_ProduceDifferentHashes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = CreateTempDirectory();
        var pathsA = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 1 });
        var pathsB = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 2 });

        await Assert.That(pathsA.ExpectedFilePath).IsNotEqualTo(pathsB.ExpectedFilePath);
    }

    /// <summary>Empty arg list does not add a hash suffix; the resolver behaves as if the
    /// args parameter were null.</summary>
    [Test]
    public async Task ResolveByTest_EmptyArgs_NoHashSuffix(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = CreateTempDirectory();
        var pathsWithEmpty = SnapshotFileResolver.ResolveByTest(dir, "C", "M", System.Array.Empty<object?>());
        var pathsWithNull = SnapshotFileResolver.ResolveByTest(dir, "C", "M");

        await Assert.That(pathsWithEmpty.ExpectedFilePath).IsEqualTo(pathsWithNull.ExpectedFilePath);
    }

    private static async Task WithExpectedFileAsync(
        string snapshotName,
        string expectedContent,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        var snapshotsDir = SnapshotFileResolver.GetDefaultSnapshotsDirectory(AppContext.BaseDirectory);
        Directory.CreateDirectory(snapshotsDir);
        var path = Path.Combine(snapshotsDir, snapshotName + ".expected.txt");
        var actualPath = Path.Combine(snapshotsDir, snapshotName + ".actual.txt");
        await File.WriteAllTextAsync(path, expectedContent, cancellationToken).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
            if (File.Exists(actualPath))
                File.Delete(actualPath);
        }
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "matches-snapshot-chain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
