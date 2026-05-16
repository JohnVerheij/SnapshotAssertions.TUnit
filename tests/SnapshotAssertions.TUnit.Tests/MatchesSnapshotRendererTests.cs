using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.Render;
using SnapshotAssertions.TUnit;
using TUnit.Assertions.Exceptions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// End-to-end tests of the renderer-projected <c>MatchesSnapshot</c> overloads added in
/// v0.4.0: <c>MatchesSnapshot(SnapshotRenderer&lt;T&gt;)</c> and
/// <c>MatchesSnapshot(Func&lt;T, string&gt;)</c>. Covers both overloads, all chain methods
/// (<c>WithName</c>, <c>AtPath</c>, <c>WithOptions</c>, <c>WithScrubber</c>), and the
/// failure modes (null source, null renderer, null render fn, renderer throws, renderer
/// returns null, source value null).
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class MatchesSnapshotRendererTests
{
    /// <summary>Subclass renderer overload + matching baseline: assertion passes.</summary>
    [Test]
    public async Task RendererOverload_MatchingContent_Passes(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "renderer-match.expected.txt");
        await File.WriteAllTextAsync(expected, "rendered:42\n", cancellationToken).ConfigureAwait(false);

        var actual = 42;
        await Assert.That(actual).MatchesSnapshot(new IntRenderer()).AtPath(expected);
    }

    /// <summary>Delegate overload + matching baseline: assertion passes.</summary>
    [Test]
    public async Task DelegateOverload_MatchingContent_Passes(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "delegate-match.expected.txt");
        await File.WriteAllTextAsync(expected, "value:7\n", cancellationToken).ConfigureAwait(false);

        var actual = 7;
        await Assert.That(actual).MatchesSnapshot(x =>
            $"value:{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n").AtPath(expected);
    }

    /// <summary>Mismatched rendered content fails with both paths in the message.</summary>
    [Test]
    public async Task MismatchedRenderedContent_FailsWithPathsInMessage(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "mismatch.expected.txt");
        await File.WriteAllTextAsync(expected, "rendered:1\n", cancellationToken).ConfigureAwait(false);

        var actual = 99;
        var ex = await Assert.That(async () =>
            await Assert.That(actual).MatchesSnapshot(new IntRenderer()).AtPath(expected)).Throws<AssertionException>();

        await Assert.That(ex!.Message).Contains("mismatch.expected.txt");
        await Assert.That(ex.Message).Contains(".actual.txt");
    }

    /// <summary>WithScrubber on the renderer-projected assertion: scrubs the rendered string,
    /// not the source value, then compares to baseline.</summary>
    [Test]
    public async Task RendererOverload_WithScrubber_AppliedToRenderedContent(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "scrub.expected.txt");
        await File.WriteAllTextAsync(expected, "id=<guid:0>\n", cancellationToken).ConfigureAwait(false);

        // The renderer emits a GUID; the scrubber replaces it with <guid:0>.
        await Assert.That("11111111-2222-3333-4444-555555555555")
            .MatchesSnapshot(x => $"id={x}\n")
            .AtPath(expected)
            .WithScrubber(Scrubbers.Guid);
    }

    /// <summary>WithOptions on the renderer-projected assertion: applies to the rendered
    /// content comparison.</summary>
    [Test]
    public async Task RendererOverload_WithOptions_AppliedToComparison(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "options.expected.txt");
        // Baseline uses CRLF; actual will be LF; SnapshotOptions.NormalizedLineEndings
        // makes them compare equal.
        await File.WriteAllTextAsync(expected, "line1\r\nline2\r\n", cancellationToken).ConfigureAwait(false);

        await Assert.That("line1\nline2\n")
            .MatchesSnapshot(x => x)
            .AtPath(expected)
            .WithOptions(SnapshotOptions.NormalizedLineEndings);
    }

    /// <summary>WithName chain method drives path resolution. Verified by asserting the
    /// supplied name appears in the resolved expected path of the failure message; if
    /// WithName were broken (didn't override the test-context-derived name), the resolved
    /// path would be the default test-method-derived one and would not contain the literal.
    /// No AtPath override so name resolution is the only path-driver under test.</summary>
    [Test]
    public async Task RendererOverload_WithName_DrivesExpectedPathResolution(CancellationToken cancellationToken)
    {
        var actual = 1;
        const string explicitName = "renderer-overload-withname-test-fixture";

        // No baseline file exists at the WithName-resolved path, so the assertion fails
        // with NoBaseline. The failure message names the resolved expected path; that
        // path must contain the WithName literal for the override to be observably wired.
        var ex = await Assert.That(async () =>
            await Assert.That(actual)
                .MatchesSnapshot(x => $"value:{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n")
                .WithName(explicitName))
            .Throws<AssertionException>();

        await Assert.That(ex!.Message).Contains($"{explicitName}.expected.txt");
    }

    [Test]
    public async Task RendererOverload_NullRenderer_ThrowsArgumentNull(CancellationToken cancellationToken)
    {
        var actual = 42;
        await Assert.That(async () =>
            await Assert.That(actual).MatchesSnapshot((SnapshotRenderer<int>)null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DelegateOverload_NullRenderFn_ThrowsArgumentNull(CancellationToken cancellationToken)
    {
        var actual = 42;
        await Assert.That(async () =>
            await Assert.That(actual).MatchesSnapshot((Func<int, string>)null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task RendererOverload_NullSourceValue_FailsAssertion(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "null.expected.txt");
        await File.WriteAllTextAsync(expected, "anything\n", cancellationToken).ConfigureAwait(false);

        string? actual = null;
        var ex = await Assert.That(async () =>
            await Assert.That(actual).MatchesSnapshot(s => s ?? "").AtPath(expected))
            .Throws<AssertionException>();
        await Assert.That(ex!.Message).Contains("actual value was null");
    }

    [Test]
    public async Task RendererOverload_RendererThrows_FailsAssertionWithThrownDetail(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "throw.expected.txt");
        await File.WriteAllTextAsync(expected, "anything\n", cancellationToken).ConfigureAwait(false);

        var actual = 42;
        var ex = await Assert.That(async () =>
            await Assert.That(actual).MatchesSnapshot(_ => throw new InvalidOperationException("intentional")).AtPath(expected))
            .Throws<AssertionException>();
        await Assert.That(ex!.Message).Contains("renderer threw");
        await Assert.That(ex.Message).Contains("InvalidOperationException");
        await Assert.That(ex.Message).Contains("intentional");
    }

    /// <summary>Source func that throws: the renderer-projected assertion's <c>CheckAsync</c>
    /// short-circuits to an <c>AssertionResult.Failed</c> that names the thrown type, mirroring
    /// the string-source <c>SnapshotAssertion</c> behaviour. Pins the exception-from-source
    /// branch of <c>RenderedSnapshotAssertion.CheckAsync</c>.</summary>
    [Test]
    public async Task RendererOverload_SourceThrows_FailsWithThrownExceptionTypeInMessage(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "source-throws.expected.txt");
        await File.WriteAllTextAsync(expected, "anything\n", cancellationToken).ConfigureAwait(false);

        // Lazy-source pattern: TUnit invokes the func inside its pipeline and the exception
        // surfaces via EvaluationMetadata.Exception. Calling source() eagerly here would
        // throw before MatchesSnapshot was ever invoked.
        Func<int> source = () => throw new InvalidOperationException("intentional source failure");
        var ex = await Assert.That(async () =>
            await Assert.That(source).MatchesSnapshot(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).AtPath(expected))
            .Throws<AssertionException>();
        await Assert.That(ex!.Message).Contains("InvalidOperationException");
    }

    [Test]
    public async Task RendererOverload_RendererReturnsNull_FailsAssertion(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "null-render.expected.txt");
        await File.WriteAllTextAsync(expected, "anything\n", cancellationToken).ConfigureAwait(false);

        var actual = 42;
        var ex = await Assert.That(async () =>
            await Assert.That(actual).MatchesSnapshot(_ => (string)null!).AtPath(expected))
            .Throws<AssertionException>();
        await Assert.That(ex!.Message).Contains("renderer returned null");
    }

    /// <summary>The two-overload pattern documented in the v0.4.0 cookbook: a sibling family
    /// package's static helper can be passed via the delegate overload without taking a
    /// reference on SnapshotAssertions. This test simulates the sibling-shape using a static
    /// method.</summary>
    [Test]
    public async Task DelegateOverload_SiblingStaticHelperShape_ComposesCleanly(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "sibling.expected.txt");
        await File.WriteAllTextAsync(expected, "FAKE-RECORD[INFO]: example\n", cancellationToken).ConfigureAwait(false);

        var record = new FakeLogRecord("INFO", "example");
        await Assert.That(record).MatchesSnapshot(FakeLogRenderer.Render).AtPath(expected);
    }

    private sealed class IntRenderer : SnapshotRenderer<int>
    {
        public override string Render(int value) =>
            $"rendered:{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n";
    }

    private sealed record FakeLogRecord(string Level, string Message);

    private static class FakeLogRenderer
    {
        // Sibling-shaped: pure static method, no SnapshotAssertions reference required. The
        // delegate-overload of MatchesSnapshot accepts this directly.
        public static string Render(FakeLogRecord record) => $"FAKE-RECORD[{record.Level}]: {record.Message}\n";
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"matches-snapshot-renderer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
