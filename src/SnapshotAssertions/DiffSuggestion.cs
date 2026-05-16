namespace SnapshotAssertions;

/// <summary>
/// One scrubber recommendation surfaced by <see cref="DiffSuggestionAnalyzer"/> on a
/// snapshot-mismatch diff. The analyzer scans the differing lines of the diff for known
/// volatile patterns (GUID canonical, GUID N-format, ISO 8601 timestamps, Unix epoch
/// milliseconds, elapsed milliseconds) and emits one suggestion per pattern that has at
/// least one hit, ordered by hit count descending.
/// </summary>
/// <param name="PatternName">A human-readable name for the matched pattern
/// (e.g. <c>"GUID"</c>, <c>"ISO 8601 timestamp"</c>, <c>"elapsed-ms value"</c>). Used in
/// the rendered failure-message suggestion line.</param>
/// <param name="Count">Number of regex matches across the differing lines of the diff.
/// Strictly positive: zero-match patterns are not surfaced.</param>
/// <param name="Recommendation">A consumer-facing recommendation string suitable for
/// direct inclusion in the failure message (e.g. <c>"Consider .WithScrubber(Scrubbers.Guid)"</c>).
/// </param>
public sealed record DiffSuggestion(string PatternName, int Count, string Recommendation);
