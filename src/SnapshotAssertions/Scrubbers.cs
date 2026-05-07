using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SnapshotAssertions;

/// <summary>
/// Built-in <see cref="SnapshotScrubber"/> factory. Each property returns a stateless instance
/// (state lives in the per-call <see cref="SnapshotScrubberState"/>), so the same property may
/// be reused across tests without cross-test interference.
/// </summary>
/// <remarks>
/// <para>The three indexed scrubbers (<see cref="Guid"/>, <see cref="Iso8601Timestamp"/>,
/// <see cref="UnixEpochMillis"/>) all use the indexed-token format <c>&lt;kind:N&gt;</c>, where
/// N is assigned by first-occurrence order within the snapshot, per kind. Recurring values map
/// to the same N. The <see cref="Default"/> preset chains all three in the order Guid →
/// Iso8601Timestamp → UnixEpochMillis (the order is deterministic but unobservable when the
/// underlying patterns do not overlap).</para>
/// <para>The <see cref="Pattern(Regex, string)"/> overload accepts a pre-compiled
/// <see cref="Regex"/>; the <see cref="Pattern(string, string)"/> overload compiles a
/// <see cref="RegexOptions.NonBacktracking"/> pattern internally. Both replace every match with
/// the literal token; no indexing is applied.</para>
/// </remarks>
public static partial class Scrubbers
{
    /// <summary>Replaces all GUIDs (8-4-4-4-12 hex format, case-insensitive) with
    /// <c>&lt;guid:N&gt;</c>; recurring GUIDs share an index. Comparison is case-insensitive
    /// (lower-case canonical form is used for index look-up).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Domain term — this property identifies the GUID scrubber, not a System.Guid value. Renaming would obscure the family-wide API of one factory property per kind.")]
    public static SnapshotScrubber Guid { get; } = new GuidScrubber();

    /// <summary>Replaces ISO 8601 timestamps (e.g. <c>2026-05-07T13:45:30Z</c>,
    /// <c>2026-05-07T13:45:30.123+02:00</c>) with <c>&lt;iso8601:N&gt;</c>; recurring timestamps
    /// share an index. Comparison is ordinal (different precisions or offsets get different
    /// indices).</summary>
    public static SnapshotScrubber Iso8601Timestamp { get; } = new Iso8601TimestampScrubber();

    /// <summary>Replaces 13-digit Unix-epoch-milliseconds numbers (the post-2001 range, up to
    /// ~year 2286) with <c>&lt;unixms:N&gt;</c>; recurring numbers share an index.</summary>
    public static SnapshotScrubber UnixEpochMillis { get; } = new UnixEpochMillisScrubber();

    /// <summary>Curated chain of <see cref="Guid"/>, <see cref="Iso8601Timestamp"/>, and
    /// <see cref="UnixEpochMillis"/>. Matches the most common volatile-value cases in a
    /// single composable scrubber.</summary>
    public static SnapshotScrubber Default { get; } = new ChainScrubber([Guid, Iso8601Timestamp, UnixEpochMillis]);

    /// <summary>Replaces every match of <paramref name="pattern"/> with the literal
    /// <paramref name="token"/>. No indexing is applied; every match becomes the same token.</summary>
    /// <param name="pattern">The regex to match against.</param>
    /// <param name="token">The replacement string. Note that regex backreferences (e.g.
    /// <c>$1</c>) are NOT interpreted — the literal characters are emitted.</param>
    /// <returns>A pattern scrubber.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static SnapshotScrubber Pattern(Regex pattern, string token)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(token);
        return new PatternScrubber(pattern, token);
    }

    /// <summary>Compiles <paramref name="pattern"/> with <see cref="RegexOptions.NonBacktracking"/>
    /// (ReDoS-resistant) and replaces every match with the literal <paramref name="token"/>.</summary>
    /// <param name="pattern">The regex pattern source.</param>
    /// <param name="token">The replacement string.</param>
    /// <returns>A pattern scrubber.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="pattern"/> is not a valid regex.</exception>
    public static SnapshotScrubber Pattern(string pattern, string token)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(token);
        var compiled = new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        return new PatternScrubber(compiled, token);
    }

    private sealed partial class GuidScrubber : SnapshotScrubber
    {
        private static readonly Regex GuidPattern = GuidRegex();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "RFC 4122 canonical GUID rendering is lowercase; matching MEL / xUnit / Verify defaults expect lowercase. Upper-casing here would surprise consumers with non-canonical baselines.")]
        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            return GuidPattern.Replace(input, m =>
            {
                var key = m.Value.ToLowerInvariant();
                var idx = state.GetOrAssignIndex("guid", key);
                return string.Create(CultureInfo.InvariantCulture, $"<guid:{idx}>");
            });
        }

        [GeneratedRegex(
            @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex GuidRegex();
    }

    private sealed partial class Iso8601TimestampScrubber : SnapshotScrubber
    {
        private static readonly Regex Iso8601Pattern = Iso8601Regex();

        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            return Iso8601Pattern.Replace(input, m =>
            {
                var idx = state.GetOrAssignIndex("iso8601", m.Value);
                return string.Create(CultureInfo.InvariantCulture, $"<iso8601:{idx}>");
            });
        }

        // ISO 8601 with required date+time, optional fractional seconds, and a Z or +HH:MM /
        // -HH:MM zone marker. Non-capturing groups (?:...) keep MA0023 happy alongside
        // NonBacktracking (which doesn't compose with ExplicitCapture in current runtime).
        // Trailing \b prevents partial matches against tokens like `2026-05-07T13:45:30Zsuffix`
        // or `2026-05-07T13:45:30+02:003` — a missing end boundary would otherwise scrub the
        // valid prefix and leave the trailing word characters dangling.
        [GeneratedRegex(
            @"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\b",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex Iso8601Regex();
    }

    private sealed partial class UnixEpochMillisScrubber : SnapshotScrubber
    {
        private static readonly Regex UnixMsPattern = UnixMsRegex();

        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            return UnixMsPattern.Replace(input, m =>
            {
                var idx = state.GetOrAssignIndex("unixms", m.Value);
                return string.Create(CultureInfo.InvariantCulture, $"<unixms:{idx}>");
            });
        }

        // 13-digit number with a non-zero leading digit, at word boundaries. Covers epoch-ms
        // from 2001-09-09T01:46:40Z (1_000_000_000_000) through year ~2286 (the maximum 13-digit
        // value 9_999_999_999_999 ms ≈ 316.88 years from 1970), the practical
        // range for today's tests. The [1-9] leading-digit class rejects all-zero / leading-zero
        // 13-digit tokens (e.g. "0000000000000") that are not real epoch-ms values, so arbitrary
        // 13-digit numeric IDs do not get false-positive scrubbed. Word-boundary anchors are
        // used instead of lookarounds because RegexOptions.NonBacktracking does not support
        // negative lookahead / lookbehind; \b also correctly excludes 13-digit substrings
        // embedded in longer word-character runs (e.g. inside a 14-digit number).
        [GeneratedRegex(
            @"\b[1-9][0-9]{12}\b",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex UnixMsRegex();
    }

    private sealed class PatternScrubber : SnapshotScrubber
    {
        private readonly Regex _pattern;
        private readonly string _token;

        public PatternScrubber(Regex pattern, string token)
        {
            _pattern = pattern;
            _token = token;
        }

        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            // Use the Replace overload that takes a literal token (no $-substitution surprises).
            return _pattern.Replace(input, _ => _token);
        }
    }

    private sealed class ChainScrubber : SnapshotScrubber
    {
        private readonly SnapshotScrubber[] _inner;

        public ChainScrubber(SnapshotScrubber[] inner)
        {
            _inner = inner;
        }

        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            var work = input;
            foreach (var s in _inner)
            {
                work = s.Apply(work, state);
            }
            return work;
        }
    }
}
