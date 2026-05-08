using System;
using System.Collections.Generic;

namespace SnapshotAssertions;

/// <summary>
/// Per-snapshot scrubber state. Maps each original volatile value (a specific GUID, a specific
/// ISO 8601 timestamp, a specific Unix-millis number, etc.) to a stable index, so the same
/// recurring value across the snapshot renders as the same indexed token. State lives for the
/// duration of a single <c>MatchesSnapshot()</c> evaluation and is discarded afterwards.
/// </summary>
public sealed class SnapshotScrubberState
{
    private readonly Dictionary<string, Dictionary<string, int>> _byKind = new(StringComparer.Ordinal);

    /// <summary>Looks up the index assigned to <paramref name="originalValue"/> within the
    /// <paramref name="kind"/> namespace. If the value has not been seen before, assigns and
    /// returns the next available index for that kind (zero-based, monotonically increasing).</summary>
    /// <param name="kind">The scrubber-defined kind namespace (e.g. <c>"guid"</c>, <c>"iso8601"</c>,
    /// <c>"unixms"</c>). Different kinds maintain independent index counters.</param>
    /// <param name="originalValue">The exact substring observed in the snapshot. Equality is
    /// ordinal: callers normalise the matched text (e.g. lower-casing GUIDs) before look-up
    /// when case-insensitive equality is desired.</param>
    /// <returns>The zero-based index of the value within its kind namespace.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public int GetOrAssignIndex(string kind, string originalValue)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(originalValue);

        if (!_byKind.TryGetValue(kind, out var map))
        {
            map = new Dictionary<string, int>(StringComparer.Ordinal);
            _byKind[kind] = map;
        }

        if (!map.TryGetValue(originalValue, out var idx))
        {
            idx = map.Count;
            map[originalValue] = idx;
        }
        return idx;
    }
}
