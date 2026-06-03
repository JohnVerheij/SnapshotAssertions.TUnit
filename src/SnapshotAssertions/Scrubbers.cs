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
/// <para>The <see cref="IndexedPattern(Regex, string)"/> overload is the indexed counterpart to
/// <see cref="Pattern(Regex, string)"/>: it reuses the same <see cref="SnapshotScrubberState"/>
/// indexed-token machinery the built-in scrubbers use, so recurring identical matched values
/// share one index (<c>&lt;kind:0&gt;</c>) while distinct values get incrementing indices. Use
/// it for a volatile value outside the built-in kinds when same-value correlation across the
/// snapshot matters; use <see cref="Pattern(Regex, string)"/> when every match should collapse
/// to a single literal token.</para>
/// </remarks>
public static partial class Scrubbers
{
    /// <summary>Replaces all GUIDs (8-4-4-4-12 hex format, case-insensitive) with
    /// <c>&lt;guid:N&gt;</c>; recurring GUIDs share an index. Comparison is case-insensitive
    /// (lower-case canonical form is used for index look-up).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Domain term: this property identifies the GUID scrubber, not a System.Guid value. Renaming would obscure the family-wide API of one factory property per kind.")]
    public static SnapshotScrubber Guid { get; } = new GuidScrubber();

    /// <summary>Replaces all GUID-N format strings (32 contiguous hex chars,
    /// <c>Guid.ToString("N")</c>) with <c>&lt;guid:N&gt;</c>. Shares the
    /// <c>"guid"</c> kind-name with <see cref="Guid"/>, so the indexed-token counter is
    /// drawn from the same pool: across a snapshot containing both canonical and N-format
    /// GUID strings, indices increment in unified first-occurrence order across both
    /// formats. Recurring N-format values get the same index. Comparison is case-insensitive
    /// (lower-case canonical form is used for index look-up).</summary>
    public static SnapshotScrubber GuidN { get; } = new GuidNScrubber();

    /// <summary>Replaces all elapsed-millisecond values (e.g. <c>42ms</c>,
    /// <c>42 ms</c>, <c>42.5ms</c>, <c>1234.567 ms</c>) with
    /// <c>&lt;elapsed-ms:N&gt;</c>. Recurring elapsed values share an index. Pattern is
    /// case-sensitive on the <c>ms</c> suffix (uppercase <c>MS</c> is not matched); use
    /// <see cref="Pattern(string, string)"/> for case-insensitive needs.</summary>
    public static SnapshotScrubber ElapsedMs { get; } = new ElapsedMsScrubber();

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

    /// <summary>Extended curated chain of <see cref="Guid"/>, <see cref="GuidN"/>,
    /// <see cref="Iso8601Timestamp"/>, <see cref="UnixEpochMillis"/>, and <see cref="ElapsedMs"/>.
    /// Superset of <see cref="Default"/>: adds GUID-N format coverage and elapsed-millisecond
    /// matching. Ordering follows the most-specific-first rule (canonical GUID before N-format
    /// to avoid hex-segment consumption; ISO 8601 before 13-digit numeric to avoid year-month
    /// component consumption).</summary>
    public static SnapshotScrubber Common { get; } = new ChainScrubber([Guid, GuidN, Iso8601Timestamp, UnixEpochMillis, ElapsedMs]);

    // Internal regex accessors for DiffSuggestionAnalyzer (same assembly). Each accessor
    // returns the SAME Regex instance the corresponding public scrubber uses, so a change
    // to the underlying regex automatically applies to the analyzer's detection without
    // creating a drift risk. The outer Scrubbers type has access to its nested private
    // classes' private fields, so no visibility promotion of the scrubber types is needed.
    internal static Regex GuidPatternRegex => GuidScrubber.GuidPattern;
    internal static Regex GuidNPatternRegex => GuidNScrubber.GuidNPattern;
    internal static Regex Iso8601PatternRegex => Iso8601TimestampScrubber.Iso8601Pattern;
    internal static Regex UnixEpochMillisPatternRegex => UnixEpochMillisScrubber.UnixMsPattern;
    internal static Regex ElapsedMsPatternRegex => ElapsedMsScrubber.ElapsedMsPattern;

    /// <summary>
    /// Combines the supplied <paramref name="scrubbers"/> into a single scrubber that applies
    /// them left-to-right. All inner scrubbers share the
    /// <see cref="SnapshotScrubberState"/> passed to <see cref="SnapshotScrubber.Apply"/>, so
    /// recurring volatile values keep stable indexed tokens across the combined pipeline.
    /// </summary>
    /// <param name="scrubbers">The scrubbers to combine. Each element must be non-<see langword="null"/>.</param>
    /// <returns>
    /// An identity scrubber (returns input unchanged) when <paramref name="scrubbers"/> is empty;
    /// the single element when the array has exactly one entry (no wrapper allocation);
    /// a chain over a defensive copy of the array otherwise. The defensive copy means later
    /// mutations of <paramref name="scrubbers"/> do not affect the returned scrubber.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="scrubbers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An element of <paramref name="scrubbers"/> is <see langword="null"/>.</exception>
    public static SnapshotScrubber Combine(params SnapshotScrubber[] scrubbers)
    {
        ArgumentNullException.ThrowIfNull(scrubbers);
        for (var i = 0; i < scrubbers.Length; i++)
        {
            if (scrubbers[i] is null)
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Scrubber at index {i} is null."),
                    nameof(scrubbers));
            }
        }

        if (scrubbers.Length is 0)
            return IdentityScrubber.Instance;
        if (scrubbers.Length is 1)
            return scrubbers[0];

        var copy = new SnapshotScrubber[scrubbers.Length];
        Array.Copy(scrubbers, copy, scrubbers.Length);
        return new ChainScrubber(copy);
    }

    /// <summary>Replaces every match of <paramref name="pattern"/> with the literal
    /// <paramref name="token"/>. No indexing is applied; every match becomes the same token.</summary>
    /// <param name="pattern">The regex to match against.</param>
    /// <param name="token">The replacement string. Note that regex backreferences (e.g.
    /// <c>$1</c>) are NOT interpreted: the literal characters are emitted.</param>
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

    /// <summary>Replaces every match of <paramref name="pattern"/> with an indexed token
    /// <c>&lt;kind:N&gt;</c> (where <c>kind</c> is <paramref name="kind"/>), reusing the same
    /// <see cref="SnapshotScrubberState"/> indexed-token machinery as the built-in scrubbers.
    /// Recurring identical matched values share the same index N; distinct values get incrementing
    /// indices in first-occurrence order. This is the indexed (correlated) counterpart to
    /// <see cref="Pattern(Regex, string)"/>, which is flat (every match collapses to one literal
    /// token, losing correlation).</summary>
    /// <param name="pattern">The regex to match against. The entire match value is the correlation
    /// key; equality is ordinal (no case folding). Use a capture-narrowing pattern when only part
    /// of the match should drive correlation.</param>
    /// <param name="kind">The kind namespace used in the emitted <c>&lt;kind:N&gt;</c> token and as
    /// the index-counter namespace on the shared state. Passing the same <paramref name="kind"/> as
    /// a built-in scrubber (e.g. <c>"guid"</c>) shares that built-in's index counter.</param>
    /// <returns>An indexed-pattern scrubber.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static SnapshotScrubber IndexedPattern(Regex pattern, string kind)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(kind);
        return new IndexedPatternScrubber(pattern, kind);
    }

    /// <summary>Compiles <paramref name="pattern"/> with <see cref="RegexOptions.NonBacktracking"/>
    /// (ReDoS-resistant) and replaces every match with an indexed token <c>&lt;kind:N&gt;</c>.
    /// Recurring identical matched values share the same index N. This is the indexed (correlated)
    /// counterpart to <see cref="Pattern(string, string)"/>.</summary>
    /// <param name="pattern">The regex pattern source.</param>
    /// <param name="kind">The kind namespace used in the emitted <c>&lt;kind:N&gt;</c> token and as
    /// the index-counter namespace on the shared state.</param>
    /// <returns>An indexed-pattern scrubber.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="pattern"/> is not a valid regex.</exception>
    public static SnapshotScrubber IndexedPattern(string pattern, string kind)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(kind);
        var compiled = new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        return new IndexedPatternScrubber(compiled, kind);
    }

    private sealed partial class GuidScrubber : SnapshotScrubber
    {
        internal static readonly Regex GuidPattern = GuidRegex();

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

    private sealed partial class GuidNScrubber : SnapshotScrubber
    {
        internal static readonly Regex GuidNPattern = GuidNRegex();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "RFC 4122 canonical GUID rendering is lowercase; matching MEL / xUnit / Verify defaults expect lowercase. Upper-casing here would surprise consumers with non-canonical baselines.")]
        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            return GuidNPattern.Replace(input, m =>
            {
                var key = m.Value.ToLowerInvariant();
                // Share the "guid" kind name with the canonical GuidScrubber so the index
                // counter is unified across the two formats: the Nth GUID occurrence in a
                // snapshot gets the same N regardless of whether it was hyphenated or N-format.
                var idx = state.GetOrAssignIndex("guid", key);
                return string.Create(CultureInfo.InvariantCulture, $"<guid:{idx}>");
            });
        }

        // 32 contiguous hex chars at word boundaries. The trailing \b prevents over-matching
        // longer hex tokens (e.g. SHA-256, 64-hex). The canonical GuidScrubber's hyphenated
        // pattern runs first in Scrubbers.Common; consumers chaining GuidN alone need to be
        // aware that a 32-hex prefix of a longer hex string is NOT a Guid:N and will not
        // match here (the \b anchor refuses match in mid-word position). Note: per the
        // family ordering rule, the canonical (hyphenated) GuidScrubber runs first so its
        // hex segments are already consumed before this pattern evaluates.
        [GeneratedRegex(
            @"\b[0-9a-fA-F]{32}\b",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex GuidNRegex();
    }

    private sealed partial class ElapsedMsScrubber : SnapshotScrubber
    {
        internal static readonly Regex ElapsedMsPattern = ElapsedMsRegex();

        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            return ElapsedMsPattern.Replace(input, m =>
            {
                var idx = state.GetOrAssignIndex("elapsed-ms", m.Value);
                return string.Create(CultureInfo.InvariantCulture, $"<elapsed-ms:{idx}>");
            });
        }

        // Integer or fixed-point number, optional whitespace, literal "ms", at word
        // boundaries. The leading \b prevents matching a digit sequence that's the tail of
        // a longer numeric token (e.g. "1234.567" inside "v1234.567ms" would still match
        // because the \b sits before the digit run; for v0.4.0 we accept this minor case as
        // out-of-scope. Consumers needing tighter matching use Scrubbers.Pattern.). The
        // trailing \b after "ms" prevents matching "msdb" or "mscorlib" tokens.
        [GeneratedRegex(
            @"\b\d+(?:\.\d+)?\s*ms\b",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex ElapsedMsRegex();
    }

    private sealed partial class Iso8601TimestampScrubber : SnapshotScrubber
    {
        internal static readonly Regex Iso8601Pattern = Iso8601Regex();

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
        // or `2026-05-07T13:45:30+02:003`: a missing end boundary would otherwise scrub the
        // valid prefix and leave the trailing word characters dangling.
        [GeneratedRegex(
            @"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\b",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex Iso8601Regex();
    }

    private sealed partial class UnixEpochMillisScrubber : SnapshotScrubber
    {
        internal static readonly Regex UnixMsPattern = UnixMsRegex();

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

    private sealed class IndexedPatternScrubber : SnapshotScrubber
    {
        private readonly Regex _pattern;
        private readonly string _kind;

        public IndexedPatternScrubber(Regex pattern, string kind)
        {
            _pattern = pattern;
            _kind = kind;
        }

        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            // Reuse the same indexed-token machinery as the built-in scrubbers: the matched value
            // is the correlation key, so recurring identical values share one index per kind.
            return _pattern.Replace(input, m =>
            {
                var idx = state.GetOrAssignIndex(_kind, m.Value);
                return string.Create(CultureInfo.InvariantCulture, $"<{_kind}:{idx}>");
            });
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

    private sealed class IdentityScrubber : SnapshotScrubber
    {
        public static readonly IdentityScrubber Instance = new();

        public override string Apply(string input, SnapshotScrubberState state)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(state);
            return input;
        }
    }
}
