using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SnapshotAssertions;

/// <summary>
/// Scans the rendered diff from a snapshot mismatch for known volatile patterns
/// (GUID, GUID N-format, ISO 8601 timestamp, Unix epoch milliseconds, elapsed ms) and
/// surfaces <see cref="DiffSuggestion"/> entries recommending applicable built-in
/// scrubbers. Pure: no IO, no allocation beyond the result list and the per-pattern
/// match enumeration.
/// </summary>
/// <remarks>
/// <para>The analyzer counts regex matches only on lines that begin with the diff
/// markers <c>+</c> or <c>-</c> (i.e. the differing lines emitted by
/// <see cref="LineDiffRenderer"/>). Context lines that match between expected and
/// actual are skipped so a pattern that appears in both sides without drift does NOT
/// surface as a suggestion.</para>
/// <para>The result list is sorted by <see cref="DiffSuggestion.Count"/> descending,
/// stable secondary ordering by pattern declaration order. Patterns with zero hits are
/// omitted. Callers that want to cap the suggestion list at a maximum count (e.g. the
/// top 3 + rollup pattern used by <see cref="SnapshotResult.WriteDescription(System.IO.TextWriter)"/>)
/// apply that cap on top of the analyzer output.</para>
/// </remarks>
public static class DiffSuggestionAnalyzer
{
    /// <summary>
    /// Scans <paramref name="diff"/> for the five built-in volatile patterns and returns
    /// matching scrubber recommendations.
    /// </summary>
    /// <param name="diff">A rendered diff string, typically the <see cref="SnapshotResult.Diff"/>
    /// property. Lines beginning with <c>+</c> or <c>-</c> are scanned; other lines are skipped.
    /// May be empty (yields an empty result list).</param>
    /// <returns>The list of suggestions, sorted by hit count descending. Empty if the diff is
    /// empty, has no differing lines, or contains no known patterns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="diff"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<DiffSuggestion> Analyze(string diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        if (diff.Length is 0)
            return [];

        var differingContent = ExtractDifferingLines(diff);
        if (differingContent.Length is 0)
            return [];

        var results = CollectMatchingCandidates(differingContent);
        SortByCountDescendingThenDeclarationOrder(results);
        return ProjectToReadOnlyList(results);
    }

    // Ordered candidate list. Declaration order is the secondary sort key when two patterns
    // have the same hit count (preserves Scrubbers.Common's most-specific-first chain order).
    private static (string Name, Regex Regex, string Recommendation)[] GetCandidates() =>
    [
        ("GUID", Scrubbers.GuidPatternRegex, "Consider .WithScrubber(Scrubbers.Guid)"),
        ("GUID (N-format)", Scrubbers.GuidNPatternRegex, "Consider .WithScrubber(Scrubbers.GuidN)"),
        ("ISO 8601 timestamp", Scrubbers.Iso8601PatternRegex, "Consider .WithScrubber(Scrubbers.Iso8601Timestamp)"),
        ("Unix epoch milliseconds", Scrubbers.UnixEpochMillisPatternRegex, "Consider .WithScrubber(Scrubbers.UnixEpochMillis)"),
        ("elapsed-ms value", Scrubbers.ElapsedMsPatternRegex, "Consider .WithScrubber(Scrubbers.ElapsedMs)"),
    ];

    private static List<(DiffSuggestion Suggestion, int Order)> CollectMatchingCandidates(string differingContent)
    {
        var candidates = GetCandidates();
        var results = new List<(DiffSuggestion, int)>(candidates.Length);
        for (var i = 0; i < candidates.Length; i++)
        {
            var (name, regex, recommendation) = candidates[i];
            var count = CountMatches(regex, differingContent);
            if (count > 0)
            {
                results.Add((new DiffSuggestion(name, count, recommendation), i));
            }
        }
        return results;
    }

    private static void SortByCountDescendingThenDeclarationOrder(List<(DiffSuggestion Suggestion, int Order)> results)
    {
        results.Sort(static (a, b) =>
        {
            var byCount = b.Suggestion.Count.CompareTo(a.Suggestion.Count);
            return byCount is not 0 ? byCount : a.Order.CompareTo(b.Order);
        });
    }

    private static DiffSuggestion[] ProjectToReadOnlyList(List<(DiffSuggestion Suggestion, int Order)> results)
    {
        var ordered = new DiffSuggestion[results.Count];
        for (var i = 0; i < results.Count; i++)
        {
            ordered[i] = results[i].Suggestion;
        }
        return ordered;
    }

    private static string ExtractDifferingLines(string diff)
    {
        // Walk the diff line-by-line. Lines that begin with '+' or '-' are differing-line
        // markers from LineDiffRenderer; everything else (context lines, blank separators,
        // truncation markers) is skipped. Returned string is the concatenation of differing
        // lines (without their marker prefix) separated by '\n'.
        var sb = new StringBuilder();
        var start = 0;
        for (var i = 0; i < diff.Length; i++)
        {
            if (diff[i] is '\n')
            {
                AppendIfDiffering(sb, diff, start, i);
                start = i + 1;
            }
        }
        // Trailing line without terminator.
        if (start < diff.Length)
        {
            AppendIfDiffering(sb, diff, start, diff.Length);
        }
        return sb.ToString();
    }

    private static void AppendIfDiffering(StringBuilder sb, string diff, int start, int end)
    {
        if (end <= start)
            return;
        var first = diff[start];
        if (first is not ('+' or '-'))
            return;
        // Strip a possible \r before \n (when the input uses CRLF line endings).
        var contentEnd = end;
        if (contentEnd > start && diff[contentEnd - 1] is '\r')
            contentEnd--;
        // Skip the marker char itself; append the rest.
        if (contentEnd > start + 1)
        {
            sb.Append(diff, start + 1, contentEnd - start - 1);
        }
        sb.Append('\n');
    }

    private static int CountMatches(Regex pattern, string content)
    {
        var count = 0;
        var match = pattern.Match(content);
        while (match.Success)
        {
            count++;
            match = match.NextMatch();
        }
        return count;
    }
}
