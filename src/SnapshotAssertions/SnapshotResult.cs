using System;
using System.Collections.Generic;
using System.IO;

namespace SnapshotAssertions;

/// <summary>
/// The outcome of a snapshot comparison, including paths to the expected baseline and (on
/// mismatch or no-baseline) the written actual file, plus a rendered line-based diff for
/// failure messages.
/// </summary>
public sealed record SnapshotResult
{
    private SnapshotResult(
        SnapshotMatchOutcome outcome,
        string expectedFilePath,
        string? actualFilePath,
        string? diff)
    {
        Outcome = outcome;
        ExpectedFilePath = expectedFilePath;
        ActualFilePath = actualFilePath;
        Diff = diff;
    }

    /// <summary>The classification of the comparison outcome.</summary>
    public SnapshotMatchOutcome Outcome { get; }

    /// <summary>Absolute path to the expected baseline file. Always populated.</summary>
    public string ExpectedFilePath { get; }

    /// <summary>Absolute path to the written <c>.actual.txt</c> file when
    /// <see cref="Outcome"/> is <see cref="SnapshotMatchOutcome.Mismatched"/> or
    /// <see cref="SnapshotMatchOutcome.NoBaseline"/>; <see langword="null"/> otherwise.</summary>
    public string? ActualFilePath { get; }

    /// <summary>Rendered line-based diff between expected and actual content when
    /// <see cref="Outcome"/> is <see cref="SnapshotMatchOutcome.Mismatched"/>;
    /// <see langword="null"/> otherwise. Format is not stable; intended for failure-message
    /// display, not programmatic parsing.</summary>
    public string? Diff { get; }

    /// <summary>Whether the comparison should be treated as a pass.
    /// <see cref="SnapshotMatchOutcome.Matched"/> and <see cref="SnapshotMatchOutcome.Accepted"/>
    /// pass; the others fail.</summary>
    public bool IsPass => Outcome is SnapshotMatchOutcome.Matched or SnapshotMatchOutcome.Accepted;

    /// <summary>Constructs a <see cref="SnapshotMatchOutcome.Matched"/> result.</summary>
    /// <param name="expectedFilePath">Absolute path to the matching expected file.</param>
    /// <returns>A passing result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expectedFilePath"/> is <see langword="null"/>.</exception>
    public static SnapshotResult Matched(string expectedFilePath)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);
        return new SnapshotResult(SnapshotMatchOutcome.Matched, expectedFilePath, actualFilePath: null, diff: null);
    }

    /// <summary>Constructs a <see cref="SnapshotMatchOutcome.Mismatched"/> result.</summary>
    /// <param name="expectedFilePath">Absolute path to the expected file.</param>
    /// <param name="actualFilePath">Absolute path to the written <c>.actual.txt</c> file.</param>
    /// <param name="diff">Rendered line-based diff for failure-message display.</param>
    /// <returns>A failing result describing the mismatch.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static SnapshotResult Mismatched(string expectedFilePath, string actualFilePath, string diff)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);
        ArgumentNullException.ThrowIfNull(actualFilePath);
        ArgumentNullException.ThrowIfNull(diff);
        return new SnapshotResult(SnapshotMatchOutcome.Mismatched, expectedFilePath, actualFilePath, diff);
    }

    /// <summary>Constructs a <see cref="SnapshotMatchOutcome.NoBaseline"/> result.</summary>
    /// <param name="expectedFilePath">Absolute path to where the expected file would be.</param>
    /// <param name="actualFilePath">Absolute path to the written <c>.actual.txt</c> file the
    /// caller can inspect and rename to <c>.expected.txt</c> to accept.</param>
    /// <returns>A failing result describing the missing baseline.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static SnapshotResult NoBaseline(string expectedFilePath, string actualFilePath)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);
        ArgumentNullException.ThrowIfNull(actualFilePath);
        return new SnapshotResult(SnapshotMatchOutcome.NoBaseline, expectedFilePath, actualFilePath, diff: null);
    }

    /// <summary>Constructs a <see cref="SnapshotMatchOutcome.Accepted"/> result.</summary>
    /// <param name="expectedFilePath">Absolute path to the now-overwritten expected file.</param>
    /// <returns>A passing result indicating the actual content was written over the baseline
    /// (accept-mode).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expectedFilePath"/> is <see langword="null"/>.</exception>
    public static SnapshotResult Accepted(string expectedFilePath)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);
        return new SnapshotResult(SnapshotMatchOutcome.Accepted, expectedFilePath, actualFilePath: null, diff: null);
    }

    /// <summary>Renders a multi-line description of the result for use in assertion failure
    /// messages and diagnostic output. Includes the expected path, the actual path (when
    /// applicable), the diff (when applicable), and accept-flow guidance.</summary>
    /// <returns>A multi-line description; format is not stable.</returns>
    public string Describe()
    {
        using var writer = new StringWriter();
        WriteDescription(writer);
        return writer.ToString();
    }

    /// <summary>Writes the same description as <see cref="Describe"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">The destination text writer.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public void WriteDescription(TextWriter writer)
    {
        System.ArgumentNullException.ThrowIfNull(writer);
        switch (Outcome)
        {
            case SnapshotMatchOutcome.Matched:
                writer.Write("Snapshot matched: ");
                writer.WriteLine(ExpectedFilePath);
                break;
            case SnapshotMatchOutcome.Accepted:
                writer.Write("Snapshot accepted (SNAPSHOT_ACCEPT=1): ");
                writer.WriteLine(ExpectedFilePath);
                break;
            case SnapshotMatchOutcome.NoBaseline:
                writer.WriteLine("Snapshot baseline does not exist.");
                writer.Write("  Expected: ");
                writer.WriteLine(ExpectedFilePath);
                writer.Write("  Actual:   ");
                writer.WriteLine(ActualFilePath);
                writer.WriteLine();
                writer.WriteLine("Inspect the actual file and rename it to .expected.txt to accept it as the baseline,");
                writer.WriteLine("or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.");
                break;
            case SnapshotMatchOutcome.Mismatched:
                writer.WriteLine("Snapshot did not match the baseline.");
                writer.Write("  Expected: ");
                writer.WriteLine(ExpectedFilePath);
                writer.Write("  Actual:   ");
                writer.WriteLine(ActualFilePath);
                if (!string.IsNullOrEmpty(Diff))
                {
                    writer.WriteLine();
                    writer.Write(Diff);
                    if (!Diff.EndsWith('\n'))
                        writer.WriteLine();
                    WriteDiffSuggestions(writer, Diff);
                }
                writer.WriteLine();
                writer.WriteLine("To accept the change, rename the actual file over the expected file,");
                writer.WriteLine("or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.");
                break;
        }
    }

    /// <summary>
    /// Top-of-suggestion-list cap. Wider diffs that match many patterns would print one
    /// suggestion block per pattern; the cap keeps the failure message scannable. When
    /// suggestions exceed the cap, the surplus is rolled up into an "and N more"
    /// summary line that points consumers at <see cref="Scrubbers.Common"/>.
    /// </summary>
    private const int SuggestionDisplayCap = 3;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Make method async", Justification = "Helper for the synchronous WriteDescription method; matches the parent's sync TextWriter contract.")]
    private static void WriteDiffSuggestions(System.IO.TextWriter writer, string diff)
    {
        var suggestions = DiffSuggestionAnalyzer.Analyze(diff);
        if (suggestions.Count is 0)
            return;

        var totalHits = SumHits(suggestions, 0);
        writer.WriteLine();
        WriteHeaderLine(writer, suggestions.Count, totalHits);
        WriteCappedSuggestionLines(writer, suggestions);
        WriteRollupOrChainHint(writer, suggestions);
    }

    private static int SumHits(IReadOnlyList<DiffSuggestion> suggestions, int startIndex)
    {
        var total = 0;
        for (var i = startIndex; i < suggestions.Count; i++)
            total += suggestions[i].Count;
        return total;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Make method async", Justification = "Helper for synchronous suggestion-rendering.")]
    private static void WriteHeaderLine(System.IO.TextWriter writer, int suggestionCount, int totalHits)
    {
        var hits = totalHits.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (suggestionCount is 1)
        {
            writer.Write("Suggestion: ");
            writer.Write(hits);
            writer.WriteLine(totalHits is 1
                ? " of 1 difference matches a known volatile pattern."
                : " differences match a known volatile pattern.");
        }
        else
        {
            writer.Write("Suggestions: ");
            writer.Write(hits);
            writer.WriteLine(" differences match known volatile patterns.");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Make method async", Justification = "Helper for synchronous suggestion-rendering.")]
    private static void WriteCappedSuggestionLines(System.IO.TextWriter writer, IReadOnlyList<DiffSuggestion> suggestions)
    {
        var displayCount = System.Math.Min(suggestions.Count, SuggestionDisplayCap);
        for (var i = 0; i < displayCount; i++)
        {
            var s = suggestions[i];
            writer.Write("  - ");
            writer.Write(s.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.Write(s.Count is 1 ? " match for " : " matches for ");
            writer.Write(s.PatternName);
            writer.Write(". ");
            writer.WriteLine(s.Recommendation);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Make method async", Justification = "Helper for synchronous suggestion-rendering.")]
    private static void WriteRollupOrChainHint(System.IO.TextWriter writer, IReadOnlyList<DiffSuggestion> suggestions)
    {
        if (suggestions.Count > SuggestionDisplayCap)
        {
            var hiddenCount = suggestions.Count - SuggestionDisplayCap;
            var hiddenHits = SumHits(suggestions, SuggestionDisplayCap);
            writer.Write("  ... and ");
            writer.Write(hiddenCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.Write(" more pattern type");
            if (hiddenCount is not 1)
                writer.Write('s');
            writer.Write(" (");
            writer.Write(hiddenHits.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.Write(" hit");
            if (hiddenHits is not 1)
                writer.Write('s');
            writer.WriteLine("). Consider .WithScrubber(Scrubbers.Common)");
        }
        else if (suggestions.Count >= 2)
        {
            writer.WriteLine("  Or use the curated chain: .WithScrubber(Scrubbers.Common)");
        }
    }
}
