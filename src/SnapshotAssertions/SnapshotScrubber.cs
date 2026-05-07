using System;

namespace SnapshotAssertions;

/// <summary>
/// Base class for snapshot text transformations applied before comparison. A scrubber receives
/// the actual content plus a per-snapshot <see cref="SnapshotScrubberState"/> and returns a
/// transformed string in which volatile substrings (GUIDs, timestamps, epoch millis, etc.) have
/// been replaced by stable tokens, so a snapshot baseline can survive multiple test runs.
/// </summary>
/// <remarks>
/// <para>Scrubbers compose left-to-right via the <c>WithScrubber</c> chain on
/// <c>MatchesSnapshot()</c>; the output of one scrubber feeds the next. The shared
/// <see cref="SnapshotScrubberState"/> tracks original-value-to-index mappings so that a
/// recurring volatile value (e.g. the same GUID appearing three times) renders as the same
/// indexed token (<c>&lt;guid:0&gt;</c>) at every site.</para>
/// <para>Custom scrubbers should derive from this class and implement
/// <see cref="Apply(string, SnapshotScrubberState)"/>. The implementation must be deterministic
/// for a given input; it must not perform IO, capture cross-test state, or rely on time.</para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1694:An abstract class should have both abstract and concrete methods", Justification = "Class chosen over interface to leave room for future shared state (e.g. a Description property surfaced in failure diagnostics) without forcing a binary-breaking conversion.")]
public abstract class SnapshotScrubber
{
    /// <summary>Applies the scrubber to <paramref name="input"/> and returns the transformed
    /// string. The supplied <paramref name="state"/> is shared across all scrubbers in the
    /// pipeline for a single snapshot evaluation; use it to maintain stable indexed tokens for
    /// recurring volatile values.</summary>
    /// <param name="input">The current pipeline content (output of the previous scrubber, or
    /// the original actual content for the first scrubber).</param>
    /// <param name="state">Per-snapshot state used for stable indexed token assignment.</param>
    /// <returns>The transformed content.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public abstract string Apply(string input, SnapshotScrubberState state);
}
