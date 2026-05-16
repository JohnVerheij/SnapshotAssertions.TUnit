using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the rendering of <see cref="SnapshotResult.Describe"/> for all four
/// <see cref="SnapshotMatchOutcome"/> values. The exact text format is documented as not
/// stable, but each outcome's description must mention the relevant paths and outcome
/// name so a developer reading the failure message can act on it.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotResultDescribeTests
{
    /// <summary>Matched outcome's description carries the "Snapshot matched" headline and
    /// the expected path. Pinning the outcome-specific headline catches header regressions
    /// that path-containment alone would miss.</summary>
    [Test]
    public async Task Matched_DescribeMentionsHeadlineAndExpectedPath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Matched("/tmp/foo.expected.txt");

        var description = result.Describe();
        await Assert.That(description).Contains("Snapshot matched");
        await Assert.That(description).Contains("/tmp/foo.expected.txt");
        await Assert.That(result.IsPass).IsTrue();
    }

    /// <summary>Accepted outcome's description carries the "Snapshot accepted" headline,
    /// mentions <c>SNAPSHOT_ACCEPT</c>, and includes the expected path.</summary>
    [Test]
    public async Task Accepted_DescribeMentionsHeadlineAndAcceptModeAndPath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Accepted("/tmp/foo.expected.txt");

        var description = result.Describe();
        await Assert.That(description).Contains("Snapshot accepted");
        await Assert.That(description).Contains("SNAPSHOT_ACCEPT");
        await Assert.That(description).Contains("/tmp/foo.expected.txt");
        await Assert.That(result.IsPass).IsTrue();
    }

    /// <summary>NoBaseline outcome's description carries the "baseline does not exist"
    /// headline, mentions both paths, and includes the rename guidance.</summary>
    [Test]
    public async Task NoBaseline_DescribeMentionsHeadlineAndBothPathsAndRenameGuidance(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.NoBaseline("/tmp/foo.expected.txt", "/tmp/foo.actual.txt");

        var description = result.Describe();
        await Assert.That(description).Contains("Snapshot baseline does not exist");
        await Assert.That(description).Contains("/tmp/foo.expected.txt");
        await Assert.That(description).Contains("/tmp/foo.actual.txt");
        await Assert.That(description).Contains("rename");
        await Assert.That(result.IsPass).IsFalse();
    }

    /// <summary>Mismatched outcome's description carries the "did not match" headline,
    /// mentions both paths, the diff content, and the rename guidance.</summary>
    [Test]
    public async Task Mismatched_DescribeMentionsHeadlineAndBothPathsAndDiff(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diff = "-old\n+new\n";
        var result = SnapshotResult.Mismatched("/tmp/foo.expected.txt", "/tmp/foo.actual.txt", diff);

        var description = result.Describe();
        await Assert.That(description).Contains("Snapshot did not match the baseline");
        await Assert.That(description).Contains("/tmp/foo.expected.txt");
        await Assert.That(description).Contains("/tmp/foo.actual.txt");
        await Assert.That(description).Contains("-old");
        await Assert.That(description).Contains("+new");
        await Assert.That(description).Contains("rename");
        await Assert.That(result.IsPass).IsFalse();
    }

    /// <summary>WriteDescription throws on null writer.</summary>
    [Test]
    public void WriteDescription_NullWriter_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Matched("/tmp/x.expected.txt");
        Assert.Throws<ArgumentNullException>(() => result.WriteDescription(null!));
    }

    /// <summary>
    /// When the rendered diff does NOT end with a newline (an edge case: a custom diff
    /// renderer or a hand-built Mismatched result), the describer appends one to keep the
    /// accept-flow guidance on its own line. Pins the <c>if (!Diff.EndsWith('\n'))</c> THEN
    /// branch, which is rarely exercised because the bundled <see cref="LineDiffRenderer"/>
    /// always emits a newline-terminated diff.
    /// </summary>
    [Test]
    public async Task WriteDescription_MismatchedWithoutNewlineTerminatedDiff_AppendsNewline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Mismatched("/tmp/x.expected.txt", "/tmp/x.actual.txt", "no-newline");

        var description = result.Describe();
        // The describer must close the diff line with a newline before the trailing blank line
        // and the accept-flow guidance, otherwise the guidance prose would render on the same
        // line as the last diff entry. StringWriter.WriteLine emits Environment.NewLine, which
        // is platform-dependent (\r\n on Windows, \n on *nix); test for either form.
        await Assert.That(description.Contains("no-newline\n", System.StringComparison.Ordinal)
            || description.Contains("no-newline\r\n", System.StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>WriteDescription writes the same content as <see cref="SnapshotResult.Describe"/>
    /// to the supplied <see cref="TextWriter"/>.</summary>
    [Test]
    public async Task WriteDescription_RoundTripsViaTextWriter(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Matched("/tmp/x.expected.txt");

        using var writer = new StringWriter();
        result.WriteDescription(writer);

        await Assert.That(writer.ToString()).IsEqualTo(result.Describe());
    }

    // ----- Smart-diff suggestions integration (v0.4.0) -----

    /// <summary>Mismatched + diff with no known volatile patterns: suggestion section is
    /// not emitted. Pins the early-return in WriteDiffSuggestions for the empty-suggestion
    /// case so existing failure messages without volatile content stay byte-identical.</summary>
    [Test]
    public async Task Mismatched_NoKnownPatterns_NoSuggestionsSection(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-only static content\n+different static content\n";
        var result = SnapshotResult.Mismatched("/tmp/a.expected.txt", "/tmp/a.actual.txt", diff);
        var description = result.Describe();
        await Assert.That(description).DoesNotContain("Suggestion");
    }

    /// <summary>Mismatched + diff with exactly one volatile pattern: single "Suggestion:"
    /// header (singular), one bullet line, no curated-chain hint.</summary>
    [Test]
    public async Task Mismatched_SinglePattern_SingleSuggestionHeader(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-id=11111111-2222-3333-4444-555555555555\n+id=22222222-2222-3333-4444-555555555555\n";
        var result = SnapshotResult.Mismatched("/tmp/a.expected.txt", "/tmp/a.actual.txt", diff);
        var description = result.Describe();
        await Assert.That(description).Contains("Suggestion: ");
        await Assert.That(description).Contains("2 matches for GUID");
        await Assert.That(description).Contains("Consider .WithScrubber(Scrubbers.Guid)");
        // No curated-chain hint when only one pattern matched.
        await Assert.That(description).DoesNotContain("Or use the curated chain");
        await Assert.That(description).DoesNotContain("Scrubbers.Common");
    }

    /// <summary>Mismatched + diff with two patterns: plural "Suggestions:" header, both
    /// bullet lines, and the curated-chain hint pointing to Scrubbers.Common.</summary>
    [Test]
    public async Task Mismatched_TwoPatterns_SuggestionsHeaderPlusCuratedChainHint(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-id=11111111-2222-3333-4444-555555555555 at=2026-05-07T13:45:30Z\n" +
                   "+id=22222222-2222-3333-4444-555555555555 at=2026-05-07T13:46:00Z\n";
        var result = SnapshotResult.Mismatched("/tmp/a.expected.txt", "/tmp/a.actual.txt", diff);
        var description = result.Describe();
        await Assert.That(description).Contains("Suggestions: ");
        await Assert.That(description).Contains("2 matches for GUID");
        await Assert.That(description).Contains("2 matches for ISO 8601 timestamp");
        await Assert.That(description).Contains("Or use the curated chain: .WithScrubber(Scrubbers.Common)");
    }

    /// <summary>Mismatched + diff with four patterns: top-3 bullet lines plus the
    /// "... and N more" rollup pointing to Scrubbers.Common. Pins the cap behaviour.</summary>
    [Test]
    public async Task Mismatched_FourPatterns_TopThreeBulletsPlusRollup(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-canonical=11111111-2222-3333-4444-555555555555\n" +
                   "-nformat=f47ac10b58cc4372a5670e02b2c3d479\n" +
                   "-at=2026-05-07T13:45:30Z\n" +
                   "-took 42ms\n";
        var result = SnapshotResult.Mismatched("/tmp/a.expected.txt", "/tmp/a.actual.txt", diff);
        var description = result.Describe();
        // Top 3 hits surfaced; 4th rolled up into "... and 1 more" line.
        await Assert.That(description).Contains("Suggestions:");
        await Assert.That(description).Contains("... and 1 more pattern type (1 hit). Consider .WithScrubber(Scrubbers.Common)");
        // Curated-chain hint is NOT emitted alongside the rollup (rollup line points to
        // Common itself).
        await Assert.That(description).DoesNotContain("Or use the curated chain");
    }

    /// <summary>Mismatched + diff with five patterns (max): top-3 bullets + rollup
    /// reporting 2 more pattern types with their combined hit count.</summary>
    [Test]
    public async Task Mismatched_FivePatterns_RollupReportsTwoMoreTypes(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-canonical=11111111-2222-3333-4444-555555555555\n" +
                   "-nformat=f47ac10b58cc4372a5670e02b2c3d479\n" +
                   "-at=2026-05-07T13:45:30Z\n" +
                   "-epoch=1714999530000\n" +
                   "-took 42ms\n";
        var result = SnapshotResult.Mismatched("/tmp/a.expected.txt", "/tmp/a.actual.txt", diff);
        var description = result.Describe();
        await Assert.That(description).Contains("... and 2 more pattern types (2 hits). Consider .WithScrubber(Scrubbers.Common)");
    }

    /// <summary>Mismatched + diff with one pattern that has TWO hits: header reports
    /// total-hits count using plural form ("differences" not "difference"). Pins the
    /// pluralisation branch in WriteHeaderLine.</summary>
    [Test]
    public async Task Mismatched_SinglePatternMultipleHits_PluralHeader(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var diff = "-a=11111111-2222-3333-4444-555555555555\n-b=22222222-2222-3333-4444-555555555555\n";
        var result = SnapshotResult.Mismatched("/tmp/a.expected.txt", "/tmp/a.actual.txt", diff);
        var description = result.Describe();
        await Assert.That(description).Contains("Suggestion: 2 differences match a known volatile pattern.");
    }

    /// <summary>Matched outcome: suggestion section never emitted (suggestions are
    /// mismatch-only).</summary>
    [Test]
    public async Task Matched_NoSuggestionsSection(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = SnapshotResult.Matched("/tmp/a.expected.txt");
        var description = result.Describe();
        await Assert.That(description).DoesNotContain("Suggestion");
    }

    /// <summary>NoBaseline outcome: suggestion section never emitted.</summary>
    [Test]
    public async Task NoBaseline_NoSuggestionsSection(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = SnapshotResult.NoBaseline("/tmp/a.expected.txt", "/tmp/a.actual.txt");
        var description = result.Describe();
        await Assert.That(description).DoesNotContain("Suggestion");
    }

    /// <summary>Accepted outcome: suggestion section never emitted.</summary>
    [Test]
    public async Task Accepted_NoSuggestionsSection(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = SnapshotResult.Accepted("/tmp/a.expected.txt");
        var description = result.Describe();
        await Assert.That(description).DoesNotContain("Suggestion");
    }

    // ----- DiffSuggestion record (v0.4.0) -----

    /// <summary>DiffSuggestion constructor exposes the three properties.</summary>
    [Test]
    public async Task DiffSuggestion_Properties_RoundTripFromConstructor(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var s = new DiffSuggestion("Test", 7, "Try this.");
        await Assert.That(s.PatternName).IsEqualTo("Test");
        await Assert.That(s.Count).IsEqualTo(7);
        await Assert.That(s.Recommendation).IsEqualTo("Try this.");
    }

    /// <summary>DiffSuggestion is a record: two instances with the same field values
    /// compare equal.</summary>
    [Test]
    public async Task DiffSuggestion_RecordEquality_SameValuesCompareEqual(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var a = new DiffSuggestion("Test", 7, "Try this.");
        var b = new DiffSuggestion("Test", 7, "Try this.");
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }
}
