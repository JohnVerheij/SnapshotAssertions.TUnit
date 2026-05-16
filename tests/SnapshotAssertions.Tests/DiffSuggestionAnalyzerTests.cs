using System;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins <see cref="DiffSuggestionAnalyzer.Analyze(string)"/>: detection of the five built-in
/// volatile patterns (GUID canonical, GUID N-format, ISO 8601 timestamp, Unix epoch
/// milliseconds, elapsed ms) inside the differing lines of a rendered diff. Also pins the
/// stable sort (count descending; secondary by declaration order) and the no-match,
/// null-input, and context-line-only edge cases. The smart-diff suggestion rendering inside
/// <c>SnapshotResult.WriteDescription</c> (top-3 cap + rollup) is covered separately.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class DiffSuggestionAnalyzerTests
{
    [Test]
    public async Task NullDiff_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => DiffSuggestionAnalyzer.Analyze(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task EmptyDiff_ReturnsEmptyList(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = DiffSuggestionAnalyzer.Analyze(string.Empty);
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DiffWithOnlyContextLines_ReturnsEmptyList(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Lines without a leading +/- marker are context lines; the analyzer skips them.
        var diff = " unchanged line one\n unchanged line two\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DiffWithMatchingPatternsInContextLines_NotSurfaced(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // A GUID that appears in a context line (no +/- marker) should NOT trigger a
        // suggestion because it didn't contribute to the mismatch.
        var diff = " context line with guid=11111111-2222-3333-4444-555555555555\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DiffWithNoKnownPatterns_ReturnsEmptyList(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-actual one\n+actual two\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DiffWithSingleGuidInDifferingLine_ReturnsOneSuggestion(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-id=11111111-2222-3333-4444-555555555555\n+id=22222222-2222-3333-4444-555555555555\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PatternName).IsEqualTo("GUID");
        await Assert.That(result[0].Count).IsEqualTo(2);
        await Assert.That(result[0].Recommendation).IsEqualTo("Consider .WithScrubber(Scrubbers.Guid)");
    }

    [Test]
    public async Task DiffWithGuidNFormat_DetectedSeparatelyFromCanonical(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // GuidN's 32-hex regex picks up N-format strings; canonical Guid's hyphenated regex
        // picks up the 8-4-4-4-12 form. Both appear in suggestions independently.
        var diff = "-canonical=11111111-2222-3333-4444-555555555555\n" +
                   "+nformat=f47ac10b58cc4372a5670e02b2c3d479\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(2);
        // Sort is count-desc with declaration order as tiebreaker; both have count=1, so
        // canonical Guid (declared first) wins the tie.
        await Assert.That(result[0].PatternName).IsEqualTo("GUID");
        await Assert.That(result[1].PatternName).IsEqualTo("GUID (N-format)");
    }

    [Test]
    public async Task DiffWithIso8601Timestamp_Detected(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-at=2026-05-07T13:45:30Z\n+at=2026-05-07T13:46:00Z\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PatternName).IsEqualTo("ISO 8601 timestamp");
        await Assert.That(result[0].Count).IsEqualTo(2);
    }

    [Test]
    public async Task DiffWithUnixEpochMillis_Detected(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-epoch=1714999530000\n+epoch=1714999531000\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PatternName).IsEqualTo("Unix epoch milliseconds");
        await Assert.That(result[0].Count).IsEqualTo(2);
    }

    [Test]
    public async Task DiffWithElapsedMs_Detected(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-took 42ms\n+took 99ms\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PatternName).IsEqualTo("elapsed-ms value");
        await Assert.That(result[0].Count).IsEqualTo(2);
    }

    [Test]
    public async Task DiffWithAllFivePatterns_ReturnsAllFiveSorted(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // All five patterns hit exactly once each. With equal counts, declaration order
        // wins: GUID, GUID-N, ISO 8601, Unix epoch ms, elapsed-ms.
        var diff = "-canonical=11111111-2222-3333-4444-555555555555\n" +
                   "-nformat=f47ac10b58cc4372a5670e02b2c3d479\n" +
                   "-at=2026-05-07T13:45:30Z\n" +
                   "-epoch=1714999530000\n" +
                   "-took 42ms\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(5);
        await Assert.That(result[0].PatternName).IsEqualTo("GUID");
        await Assert.That(result[1].PatternName).IsEqualTo("GUID (N-format)");
        await Assert.That(result[2].PatternName).IsEqualTo("ISO 8601 timestamp");
        await Assert.That(result[3].PatternName).IsEqualTo("Unix epoch milliseconds");
        await Assert.That(result[4].PatternName).IsEqualTo("elapsed-ms value");
    }

    [Test]
    public async Task DiffWithUnequalCounts_SortedByCountDescending(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // 3 GUIDs, 2 timestamps, 1 elapsed-ms: result must be ordered 3, 2, 1.
        var diff = "-g=11111111-2222-3333-4444-555555555555\n" +
                   "-g=22222222-2222-3333-4444-555555555555\n" +
                   "-g=33333333-2222-3333-4444-555555555555\n" +
                   "-at=2026-05-07T13:45:30Z\n" +
                   "-at=2026-05-07T13:46:00Z\n" +
                   "-took 42ms\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].Count).IsEqualTo(3);
        await Assert.That(result[0].PatternName).IsEqualTo("GUID");
        await Assert.That(result[1].Count).IsEqualTo(2);
        await Assert.That(result[1].PatternName).IsEqualTo("ISO 8601 timestamp");
        await Assert.That(result[2].Count).IsEqualTo(1);
        await Assert.That(result[2].PatternName).IsEqualTo("elapsed-ms value");
    }

    [Test]
    public async Task DiffWithCrlfLineEndings_PatternStillDetected(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-id=11111111-2222-3333-4444-555555555555\r\n+id=22222222-2222-3333-4444-555555555555\r\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Count).IsEqualTo(2);
    }

    [Test]
    public async Task DiffWithoutTrailingNewline_FinalLineStillScanned(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // The last differing line lacks a terminating \n; the analyzer must still scan it.
        var diff = "-id=11111111-2222-3333-4444-555555555555";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Count).IsEqualTo(1);
    }

    [Test]
    public async Task DiffWithMixedContextAndDifferingLines_OnlyDifferingScanned(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Context line has a GUID that's IGNORED; differing line has one that's counted.
        var diff = " context guid=11111111-2222-3333-4444-555555555555\n" +
                   "-removed guid=22222222-2222-3333-4444-555555555555\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Count).IsEqualTo(1);
    }

    [Test]
    public async Task DiffWithBlankDifferingLine_SkippedGracefully(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // A line with only a marker but no content (e.g. "-" or "+" then newline) is
        // emitted by LineDiffRenderer for blank-line differences. Analyzer must tolerate.
        var diff = "-\n+\n";
        var result = DiffSuggestionAnalyzer.Analyze(diff);
        await Assert.That(result.Count).IsEqualTo(0);
    }
}
