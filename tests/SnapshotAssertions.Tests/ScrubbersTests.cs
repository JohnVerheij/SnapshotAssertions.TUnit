using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the <see cref="Scrubbers"/> built-ins, the <see cref="Scrubbers.Default"/> chain, the
/// indexed-token assignment scheme, and the <see cref="Scrubbers.Pattern(string, string)"/>
/// factory. End-to-end integration with <c>MatchesSnapshot()</c> is exercised via separate
/// chain tests; this file covers the framework-agnostic scrubbing logic in isolation.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class ScrubbersTests
{
    // ----- Guid scrubber -----

    [Test]
    public async Task Guid_SingleOccurrence_ReplacedWithIndexZero(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "id=11111111-2222-3333-4444-555555555555 done";
        var output = Scrubbers.Guid.Apply(input, state);
        await Assert.That(output).IsEqualTo("id=<guid:0> done");
    }

    [Test]
    public async Task Guid_RecurringValue_SharesSameIndex(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var g = "11111111-2222-3333-4444-555555555555";
        var input = $"first {g}; again {g}; once more {g}";
        var output = Scrubbers.Guid.Apply(input, state);
        await Assert.That(output).IsEqualTo("first <guid:0>; again <guid:0>; once more <guid:0>");
    }

    [Test]
    public async Task Guid_DifferentValues_GetDifferentIndices(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "first=11111111-1111-1111-1111-111111111111 second=22222222-2222-2222-2222-222222222222";
        var output = Scrubbers.Guid.Apply(input, state);
        await Assert.That(output).IsEqualTo("first=<guid:0> second=<guid:1>");
    }

    [Test]
    public async Task Guid_CaseInsensitive_SameValueSharesIndex(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "lower=abcdef00-1234-5678-9abc-def012345678 upper=ABCDEF00-1234-5678-9ABC-DEF012345678";
        var output = Scrubbers.Guid.Apply(input, state);
        await Assert.That(output).IsEqualTo("lower=<guid:0> upper=<guid:0>");
    }

    [Test]
    public async Task Guid_NoMatches_PassesThroughUnchanged(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "no guid here, not-a-guid=12345";
        var output = Scrubbers.Guid.Apply(input, state);
        await Assert.That(output).IsEqualTo(input);
    }

    // ----- Iso8601Timestamp scrubber -----

    [Test]
    public async Task Iso8601_BasicZ_ReplacedWithIndexedToken(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "started=2026-05-07T13:45:30Z";
        var output = Scrubbers.Iso8601Timestamp.Apply(input, state);
        await Assert.That(output).IsEqualTo("started=<iso8601:0>");
    }

    [Test]
    public async Task Iso8601_FractionalSecondsAndOffset_Match(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "ts=2026-05-07T13:45:30.123+02:00";
        var output = Scrubbers.Iso8601Timestamp.Apply(input, state);
        await Assert.That(output).IsEqualTo("ts=<iso8601:0>");
    }

    [Test]
    public async Task Iso8601_DifferentTimestamps_GetDifferentIndices(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "a=2026-05-07T13:45:30Z b=2026-05-07T13:45:31Z c=2026-05-07T13:45:30Z";
        var output = Scrubbers.Iso8601Timestamp.Apply(input, state);
        await Assert.That(output).IsEqualTo("a=<iso8601:0> b=<iso8601:1> c=<iso8601:0>");
    }

    // ----- UnixEpochMillis scrubber -----

    [Test]
    public async Task UnixEpochMillis_ThirteenDigitNumber_Replaced(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "epoch=1746619530000 stop";
        var output = Scrubbers.UnixEpochMillis.Apply(input, state);
        await Assert.That(output).IsEqualTo("epoch=<unixms:0> stop");
    }

    [Test]
    public async Task UnixEpochMillis_TwelveOrFourteenDigits_Untouched(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "twelve=174661953000 fourteen=17466195300000";
        var output = Scrubbers.UnixEpochMillis.Apply(input, state);
        await Assert.That(output).IsEqualTo(input);
    }

    [Test]
    public async Task UnixEpochMillis_SameNumberRecurring_SharesIndex(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "a=1746619530000 b=1746619530000 c=1746619540000";
        var output = Scrubbers.UnixEpochMillis.Apply(input, state);
        await Assert.That(output).IsEqualTo("a=<unixms:0> b=<unixms:0> c=<unixms:1>");
    }

    // ----- Default chain -----

    [Test]
    public async Task Default_AppliesAllThreeBuiltins(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "id=11111111-2222-3333-4444-555555555555 ts=2026-05-07T13:45:30Z ms=1746619530000";
        var output = Scrubbers.Default.Apply(input, state);
        await Assert.That(output).IsEqualTo("id=<guid:0> ts=<iso8601:0> ms=<unixms:0>");
    }

    [Test]
    public async Task Default_KindNamespacesAreIndependent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        // Each kind starts indexing at 0 independently.
        var input = "g1=11111111-1111-1111-1111-111111111111 g2=22222222-2222-2222-2222-222222222222 t1=2026-05-07T13:45:30Z t2=2026-05-07T13:45:31Z";
        var output = Scrubbers.Default.Apply(input, state);
        await Assert.That(output).IsEqualTo("g1=<guid:0> g2=<guid:1> t1=<iso8601:0> t2=<iso8601:1>");
    }

    // ----- Pattern factory -----

    [Test]
    public async Task Pattern_StringOverload_ReplacesAllMatches(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var scrubber = Scrubbers.Pattern(@"\bsecret-[a-z]+\b", "<secret>");
        var input = "header=secret-foo body=secret-bar trailer";
        var output = scrubber.Apply(input, state);
        await Assert.That(output).IsEqualTo("header=<secret> body=<secret> trailer");
    }

    [Test]
    public async Task Pattern_RegexOverload_ReplacesAllMatches(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var rx = new Regex(@"\bv[0-9]+\.[0-9]+\.[0-9]+\b", RegexOptions.NonBacktracking);
        var scrubber = Scrubbers.Pattern(rx, "<version>");
        var input = "from v1.2.3 to v4.5.6";
        var output = scrubber.Apply(input, state);
        await Assert.That(output).IsEqualTo("from <version> to <version>");
    }

    [Test]
    public async Task Pattern_RegexBackreferenceInToken_TreatedLiterally(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        // The replacement uses Replace(input, _ => token), so $1-style references are NOT
        // interpreted as backreferences: they emit literally. This pin guards against an
        // accidental refactor to Regex.Replace(input, token) which DOES interpret $1.
        var scrubber = Scrubbers.Pattern(@"\bid=\d+\b", "id=$1<scrubbed>");
        var input = "begin id=42 end";
        var output = scrubber.Apply(input, state);
        await Assert.That(output).IsEqualTo("begin id=$1<scrubbed> end");
    }

    // ----- State helpers -----

    [Test]
    public async Task State_GetOrAssignIndex_AssignsZeroOnFirstCall(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        await Assert.That(state.GetOrAssignIndex("kind", "value-a")).IsEqualTo(0);
    }

    [Test]
    public async Task State_GetOrAssignIndex_ReturnsSameIndexForSameValue(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var first = state.GetOrAssignIndex("kind", "value-a");
        var second = state.GetOrAssignIndex("kind", "value-a");
        await Assert.That(second).IsEqualTo(first);
    }

    [Test]
    public async Task State_GetOrAssignIndex_DifferentValuesGetDifferentIndices(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var a = state.GetOrAssignIndex("kind", "a");
        var b = state.GetOrAssignIndex("kind", "b");
        await Assert.That(a).IsEqualTo(0);
        await Assert.That(b).IsEqualTo(1);
    }

    [Test]
    public async Task State_DifferentKinds_HaveIndependentCounters(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var firstOfKindA = state.GetOrAssignIndex("a", "x");
        var firstOfKindB = state.GetOrAssignIndex("b", "x");
        await Assert.That(firstOfKindA).IsEqualTo(0);
        await Assert.That(firstOfKindB).IsEqualTo(0);
    }

    // ----- Argument validation -----

    [Test]
    public async Task Pattern_NullPattern_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => Scrubbers.Pattern((Regex)null!, "<x>")).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Pattern_NullToken_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var rx = new Regex("foo", RegexOptions.NonBacktracking);
        await Assert.That(() => Scrubbers.Pattern(rx, null!)).Throws<ArgumentNullException>();
    }

    // ----- Combine factory -----

    [Test]
    public async Task Combine_EmptyArray_ReturnsIdentityScrubber(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var scrubber = Scrubbers.Combine();
        var input = "id=11111111-2222-3333-4444-555555555555 ts=2026-05-07T13:45:30Z";
        var output = scrubber.Apply(input, state);
        // Identity: input passes through unchanged regardless of content.
        await Assert.That(output).IsEqualTo(input);
    }

    [Test]
    public async Task Combine_SingleScrubber_ReturnsTheElementUnchanged(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // No wrapper allocation on the single-element fast path. Verified by reference
        // equality: the combined result IS the input element.
        var combined = Scrubbers.Combine(Scrubbers.Guid);
        await Assert.That(combined).IsSameReferenceAs(Scrubbers.Guid);
    }

    [Test]
    public async Task Combine_MultipleScrubbers_AppliesLeftToRight(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var scrubber = Scrubbers.Combine(Scrubbers.Guid, Scrubbers.Iso8601Timestamp);
        var input = "id=11111111-2222-3333-4444-555555555555 ts=2026-05-07T13:45:30Z";
        var output = scrubber.Apply(input, state);
        await Assert.That(output).IsEqualTo("id=<guid:0> ts=<iso8601:0>");
    }

    [Test]
    public async Task Combine_AllInnerScrubbersShareState(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        // Build a chain Default + custom-mask. Recurring GUIDs / timestamps must keep stable
        // indices across the combined scrubber, proving that the state is threaded through
        // every inner scrubber instead of each one starting fresh.
        var scrubber = Scrubbers.Combine(Scrubbers.Default, Scrubbers.Pattern(@"\bsecret-[a-z]+\b", "<secret>"));
        var g = "11111111-2222-3333-4444-555555555555";
        var input = $"first {g} then secret-foo and again {g}";
        var output = scrubber.Apply(input, state);
        await Assert.That(output).IsEqualTo("first <guid:0> then <secret> and again <guid:0>");
    }

    [Test]
    public async Task Combine_NullArray_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => Scrubbers.Combine((SnapshotScrubber[])null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Combine_NullElement_ThrowsArgumentException(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => Scrubbers.Combine(Scrubbers.Guid, null!, Scrubbers.Iso8601Timestamp))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Combine_DefensiveCopy_LaterMutationDoesNotAffectResult(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        // Caller passes an explicit array, then later mutates it. The combined scrubber
        // must reflect the snapshot at construction time, not the mutated state.
        var array = new[] { Scrubbers.Guid, Scrubbers.Iso8601Timestamp };
        var scrubber = Scrubbers.Combine(array);
        array[0] = Scrubbers.Pattern(@".+", "TOTALLY-DIFFERENT");
        var input = "id=11111111-2222-3333-4444-555555555555 ts=2026-05-07T13:45:30Z";
        var output = scrubber.Apply(input, state);
        await Assert.That(output).IsEqualTo("id=<guid:0> ts=<iso8601:0>");
    }

    [Test]
    public async Task Combine_EmptyArray_Apply_NullInputThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Pins that the identity-scrubber returned for an empty array honours the
        // SnapshotScrubber.Apply non-null contract on its input argument; the input null-check
        // branch in IdentityScrubber.Apply is otherwise unreachable from valid client code.
        var scrubber = Scrubbers.Combine();
        var state = new SnapshotScrubberState();
        await Assert.That(() => scrubber.Apply(null!, state)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Combine_EmptyArray_Apply_NullStateThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Same as above, for the state null-check branch.
        var scrubber = Scrubbers.Combine();
        await Assert.That(() => scrubber.Apply("input", null!)).Throws<ArgumentNullException>();
    }

    // ----- GuidN scrubber (v0.4.0) -----

    [Test]
    public async Task GuidN_SingleOccurrence_ReplacedWithIndexZero(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "id=f47ac10b58cc4372a5670e02b2c3d479 done";
        var output = Scrubbers.GuidN.Apply(input, state);
        await Assert.That(output).IsEqualTo("id=<guid:0> done");
    }

    [Test]
    public async Task GuidN_RecurringValue_SharesSameIndex(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "a=f47ac10b58cc4372a5670e02b2c3d479 b=f47ac10b58cc4372a5670e02b2c3d479";
        var output = Scrubbers.GuidN.Apply(input, state);
        await Assert.That(output).IsEqualTo("a=<guid:0> b=<guid:0>");
    }

    [Test]
    public async Task GuidN_DifferentValues_GetDifferentIndices(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "a=f47ac10b58cc4372a5670e02b2c3d479 b=00000000000000000000000000000001";
        var output = Scrubbers.GuidN.Apply(input, state);
        await Assert.That(output).IsEqualTo("a=<guid:0> b=<guid:1>");
    }

    [Test]
    public async Task GuidN_CaseInsensitive_SameValueSharesIndex(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "lower=f47ac10b58cc4372a5670e02b2c3d479 upper=F47AC10B58CC4372A5670E02B2C3D479";
        var output = Scrubbers.GuidN.Apply(input, state);
        await Assert.That(output).IsEqualTo("lower=<guid:0> upper=<guid:0>");
    }

    [Test]
    public async Task GuidN_NoMatches_PassesThroughUnchanged(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "no hex here, just words and 1234 numbers";
        var output = Scrubbers.GuidN.Apply(input, state);
        await Assert.That(output).IsEqualTo(input);
    }

    [Test]
    public async Task GuidN_ThirtyOneOrThirtyThreeHex_NotMatched(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // 31 chars and 33 chars should NOT match (only 32-char hex tokens at word boundaries).
        var state = new SnapshotScrubberState();
        var input = "short=f47ac10b58cc4372a5670e02b2c3d47 long=f47ac10b58cc4372a5670e02b2c3d4790";
        var output = Scrubbers.GuidN.Apply(input, state);
        await Assert.That(output).IsEqualTo(input);
    }

    [Test]
    public async Task GuidN_SharedKindWithCanonicalGuid_UnifiedIndexCounter(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Critical contract: GuidN shares the "guid" kind name with Scrubbers.Guid, so
        // a chain that runs both scrubbers gets one unified index counter. The Nth GUID
        // occurrence (canonical or N) gets index N, not separate index spaces per format.
        var state = new SnapshotScrubberState();
        var input = "a=11111111-2222-3333-4444-555555555555 b=f47ac10b58cc4372a5670e02b2c3d479";
        var afterGuid = Scrubbers.Guid.Apply(input, state);
        var output = Scrubbers.GuidN.Apply(afterGuid, state);
        await Assert.That(output).IsEqualTo("a=<guid:0> b=<guid:1>");
    }

    [Test]
    public async Task GuidN_NullInput_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        await Assert.That(() => Scrubbers.GuidN.Apply(null!, state)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task GuidN_NullState_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => Scrubbers.GuidN.Apply("input", null!)).Throws<ArgumentNullException>();
    }

    // ----- ElapsedMs scrubber (v0.4.0) -----

    [Test]
    public async Task ElapsedMs_Integer_ReplacedWithIndexedToken(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var output = Scrubbers.ElapsedMs.Apply("took 42ms", state);
        await Assert.That(output).IsEqualTo("took <elapsed-ms:0>");
    }

    [Test]
    public async Task ElapsedMs_Decimal_Replaced(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var output = Scrubbers.ElapsedMs.Apply("took 42.5ms", state);
        await Assert.That(output).IsEqualTo("took <elapsed-ms:0>");
    }

    [Test]
    public async Task ElapsedMs_WithSpaceBeforeUnit_Replaced(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var output = Scrubbers.ElapsedMs.Apply("took 42 ms", state);
        await Assert.That(output).IsEqualTo("took <elapsed-ms:0>");
    }

    [Test]
    public async Task ElapsedMs_RecurringValue_SharesIndex(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var output = Scrubbers.ElapsedMs.Apply("first=42ms second=42ms", state);
        await Assert.That(output).IsEqualTo("first=<elapsed-ms:0> second=<elapsed-ms:0>");
    }

    [Test]
    public async Task ElapsedMs_DifferentValues_GetDifferentIndices(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var output = Scrubbers.ElapsedMs.Apply("first=42ms second=99.9ms", state);
        await Assert.That(output).IsEqualTo("first=<elapsed-ms:0> second=<elapsed-ms:1>");
    }

    [Test]
    public async Task ElapsedMs_CaseSensitiveOnMs_UppercaseMSNotMatched(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        // The conservative pattern is case-sensitive on "ms" to avoid matching tokens like
        // "MSdb=42" or column-name fragments containing uppercase MS.
        var input = "took 42MS";
        var output = Scrubbers.ElapsedMs.Apply(input, state);
        await Assert.That(output).IsEqualTo(input);
    }

    [Test]
    public async Task ElapsedMs_TokenStartingWithMs_NotMatched(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        // Trailing \b on "ms" prevents matching "msdb" or "mscorlib".
        var input = "loaded 42 mscorlib";
        var output = Scrubbers.ElapsedMs.Apply(input, state);
        await Assert.That(output).IsEqualTo(input);
    }

    [Test]
    public async Task ElapsedMs_NoMatches_PassesThroughUnchanged(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "no durations in this string";
        var output = Scrubbers.ElapsedMs.Apply(input, state);
        await Assert.That(output).IsEqualTo(input);
    }

    [Test]
    public async Task ElapsedMs_NullInput_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        await Assert.That(() => Scrubbers.ElapsedMs.Apply(null!, state)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ElapsedMs_NullState_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => Scrubbers.ElapsedMs.Apply("input", null!)).Throws<ArgumentNullException>();
    }

    // ----- Common chain (v0.4.0) -----

    [Test]
    public async Task Common_AppliesAllFiveBuiltins(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        var input = "id=11111111-2222-3333-4444-555555555555 " +
                    "idn=f47ac10b58cc4372a5670e02b2c3d479 " +
                    "ts=2026-05-07T13:45:30Z " +
                    "epoch=1714999530000 " +
                    "elapsed=42ms";
        var output = Scrubbers.Common.Apply(input, state);
        // Shared "guid" kind: canonical first (0), N-format second (1).
        // Independent kind spaces for the other patterns.
        await Assert.That(output).IsEqualTo(
            "id=<guid:0> idn=<guid:1> ts=<iso8601:0> epoch=<unixms:0> elapsed=<elapsed-ms:0>");
    }

    [Test]
    public async Task Common_OrderingPreventsHexSegmentCollision(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Pins the most-specific-first ordering: canonical Guid (hyphenated) runs before
        // GuidN (32-hex). If GuidN ran first, the hex segments of the hyphenated form
        // would be partially consumed (each hex segment is < 32 chars so \b would prevent
        // direct consumption, but the rule still matters for documentation).
        var state = new SnapshotScrubberState();
        var output = Scrubbers.Common.Apply("g=11111111-2222-3333-4444-555555555555", state);
        await Assert.That(output).IsEqualTo("g=<guid:0>");
    }

    [Test]
    public async Task Common_KindNamespacesAreIndependent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Same numeric value rendered as both a Unix epoch ms and an elapsed-ms duration
        // gets independent indices because the two scrubbers use different kind names.
        var state = new SnapshotScrubberState();
        var output = Scrubbers.Common.Apply("epoch=1714999530000 dur=1714999530000ms", state);
        await Assert.That(output).IsEqualTo("epoch=<unixms:0> dur=<elapsed-ms:0>");
    }

    [Test]
    public async Task Common_NullInput_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = new SnapshotScrubberState();
        await Assert.That(() => Scrubbers.Common.Apply(null!, state)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Common_NullState_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => Scrubbers.Common.Apply("input", null!)).Throws<ArgumentNullException>();
    }
}
