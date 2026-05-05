using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;
using TUnit.Assertions.Exceptions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// End-to-end tests of the <c>MatchesSnapshot()</c> / <c>MatchesSnapshotFile()</c> assertion
/// chain. Each test uses a fresh temp directory and the <c>.AtPath()</c> chain (or the
/// <c>MatchesSnapshotFile(path)</c> shorthand) so the assertion is fully isolated from
/// <c>TestContext</c>-based path resolution.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class MatchesSnapshotTests
{
    /// <summary>Matching content + existing baseline: assertion passes.</summary>
    [Test]
    public async Task MatchingContent_ChainAtPath_Passes(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "match.expected.txt");
        await File.WriteAllTextAsync(expected, "hello\n", cancellationToken).ConfigureAwait(false);

        await Assert.That("hello\n").MatchesSnapshotFile(expected);
    }

    /// <summary>Matching content via the <c>.AtPath()</c> chain method on the no-arg entry.</summary>
    [Test]
    public async Task MatchingContent_AtPathChain_Passes(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "atpath.expected.txt");
        await File.WriteAllTextAsync(expected, "world\n", cancellationToken).ConfigureAwait(false);

        await Assert.That("world\n").MatchesSnapshot().AtPath(expected);
    }

    /// <summary>Mismatched content: assertion fails, writes the actual file, and the failure
    /// message includes both the expected and actual file paths plus the rendered diff so
    /// the developer can locate and resolve the mismatch from the test log alone.</summary>
    [Test]
    public async Task MismatchedContent_FailsWithBothPathsAndDiffInMessage(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "diff.expected.txt");
        var actual = Path.Combine(dir, "diff.actual.txt");
        await File.WriteAllTextAsync(expected, "hello\n", cancellationToken).ConfigureAwait(false);

        var exception = await Assert.That(async () => await Assert.That("world\n").MatchesSnapshotFile(expected))
            .Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains(expected);
        await Assert.That(exception.Message).Contains(actual);
        await Assert.That(exception.Message).Contains("hello");
        await Assert.That(exception.Message).Contains("world");

        await Assert.That(File.Exists(actual)).IsTrue();
        var actualContent = await File.ReadAllTextAsync(actual, cancellationToken).ConfigureAwait(false);
        await Assert.That(actualContent).IsEqualTo("world\n");
    }

    /// <summary>Missing baseline: assertion fails and writes the actual file so the user can
    /// inspect and rename it to the expected baseline.</summary>
    [Test]
    public async Task NoBaseline_FailsAndWritesActual(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "missing.expected.txt");
        var actual = Path.Combine(dir, "missing.actual.txt");

        await Assert.That(async () => await Assert.That("first\n").MatchesSnapshotFile(expected))
            .Throws<AssertionException>();

        await Assert.That(File.Exists(actual)).IsTrue();
    }

    /// <summary>Options chain: NormalizedLineEndings preset matches LF and CRLF baselines.</summary>
    [Test]
    public async Task WithOptionsChain_NormalizedLineEndings_AcceptsCrlfBaseline(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "crlf.expected.txt");
        await File.WriteAllTextAsync(expected, "a\r\nb\r\n", cancellationToken).ConfigureAwait(false);

        await Assert.That("a\nb\n")
            .MatchesSnapshotFile(expected, SnapshotOptions.NormalizedLineEndings);
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "matches-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
