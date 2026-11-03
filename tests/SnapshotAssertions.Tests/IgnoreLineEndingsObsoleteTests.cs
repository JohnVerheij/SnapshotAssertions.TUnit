using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Tests for the deprecated <see cref="SnapshotLineEndingMode.IgnoreLineEndings"/> value. The comparison form removes
/// line breaks as before, so old one-line baselines keep matching; the written form (actual file, accepted baseline)
/// keeps line breaks, joined with LF. The value is removed in 0.10.0. Before 0.9.0 the
/// <see cref="SnapshotOptions.NormalizedLineEndings"/> preset used this value; the options below reproduce that old
/// preset explicitly.
/// </summary>
[SuppressMessage(
    "Info Code Smell",
    "S1133:Deprecated code should be removed",
    Justification = "These tests cover the deprecated value for as long as it ships; both are removed together in 0.10.0.")]
[Obsolete("Covers the deprecated IgnoreLineEndings value, which is removed in 0.10.0.")]
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class IgnoreLineEndingsObsoleteTests
{
    /// <summary>Custom options: <see cref="SnapshotLineEndingMode.IgnoreLineEndings"/> with the
    /// strict <see cref="SnapshotTrailingNewline.Required"/> policy. Pins both the
    /// IgnoreLineEndings case in <c>ResolveSeparator</c> (the only case that returns an empty
    /// separator) AND the <c>separator.Length is 0</c> branch in
    /// <c>ApplyTrailingNewlinePolicy</c>'s Required arm, which appends a stable LF marker so
    /// trailing-newline-presence remains observable across the otherwise-flattened content.</summary>
    [Test]
    public async Task IgnoreLineEndingsWithRequiredPolicy_PreservesTrailingNewlineSemantics(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var opts = new SnapshotOptions
        {
            LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings,
            TrailingNewline = SnapshotTrailingNewline.Required,
        };
        // Both have a trailing newline; line breaks are stripped; the LF marker preserves the
        // "ended in newline" semantics so both inputs collapse to the same normalized form.
        await Assert.That(SnapshotComparer.AreEqual("a\nb\n", "a\r\nb\r\n", opts)).IsTrue();
        // One has trailing newline, one does not: under Required + IgnoreLineEndings the LF
        // marker is appended only to the first side, so the normalized forms differ.
        await Assert.That(SnapshotComparer.AreEqual("a\nb\n", "a\nb", opts)).IsFalse();
    }

    /// <summary>The combination of <c>IgnoreLineEndings</c> + <c>Required</c> + a trailing
    /// newline on the input drives the empty-separator branch of
    /// <c>ApplyTrailingNewlinePolicy</c> (which appends a stable LF marker so the
    /// trailing-newline state is preserved despite the line-separator being empty).</summary>
    [Test]
    public async Task RequiredTrailingNewline_WithIgnoreLineEndings_PreservesTrailingState(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with
        {
            LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings,
            TrailingNewline = SnapshotTrailingNewline.Required,
        };
        // Both have a trailing newline -> Required-policy path with separator.Length == 0
        // appends an LF marker on both, equality holds.
        await Assert.That(SnapshotComparer.AreEqual("a\nb\n", "a\r\nb\r\n", options)).IsTrue();
        // One has trailing, the other doesn't -> the LF marker is appended on one side only,
        // the comparison reports a mismatch (preserves Required semantics under empty separator).
        await Assert.That(SnapshotComparer.AreEqual("a\nb\n", "a\r\nb", options)).IsFalse();
    }

    /// <summary>The normalized form under <c>IgnoreLineEndings</c>, which the evaluator writes to the actual file and an
    /// accepted baseline: line breaks and blank lines are removed, a final newline is kept as one LF under the default
    /// <see cref="SnapshotTrailingNewline.Required"/>, and <see cref="SnapshotTrailingNewline.Optional"/> drops it. The README and the
    /// <see cref="SnapshotLineEndingMode.IgnoreLineEndings"/> docs describe these exact forms.</summary>
    [Test]
    public async Task IgnoreLineEndings_NormalizedForm_KeepsFinalNewlineUnlessOptional(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var required = SnapshotOptions.Default with { LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings };
        await Assert.That(SnapshotComparer.Normalize("a\nb\n", required)).IsEqualTo("ab\n");
        await Assert.That(SnapshotComparer.Normalize("a\r\nb\n\nc\r\n", required)).IsEqualTo("abc\n");
        await Assert.That(SnapshotComparer.Normalize("a\nb", required)).IsEqualTo("ab");
        await Assert.That(SnapshotComparer.Normalize("a\r\nb\n\nc\r\n", LegacyPreset)).IsEqualTo("abc");
    }

    /// <summary>Forbidden combined with IgnoreLineEndings: line breaks inside the content are ignored,
    /// but a trailing newline (carried by the LF marker the empty-separator path appends) still fails
    /// the comparison.</summary>
    [Test]
    public async Task ForbiddenTrailingNewline_WithIgnoreLineEndings_StillFailsOnTrailingNewline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with
        {
            LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings,
            TrailingNewline = SnapshotTrailingNewline.Forbidden,
        };
        await Assert.That(SnapshotComparer.AreEqual("a\r\nb", "a\nb", options)).IsTrue();
        await Assert.That(SnapshotComparer.AreEqual("a\r\nb\r\n", "a\nb", options)).IsFalse();
        await Assert.That(SnapshotComparer.AreEqual("a\r\nb\r\n", "a\nb\n", options)).IsFalse();
    }

    /// <summary>The pre-0.9.0 meaning of the <see cref="SnapshotOptions.NormalizedLineEndings"/> preset, spelled out:
    /// line breaks removed and the final newline ignored, so <c>"a\nb"</c> equals <c>"ab"</c>.</summary>
    [Test]
    public async Task LegacyPresetOptions_RemoveLineBreaksAndIgnoreFinalNewline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await Assert.That(SnapshotComparer.AreEqual("a\r\nb\nc\r", "abc", LegacyPreset)).IsTrue();
        await Assert.That(SnapshotComparer.AreEqual("a\nb\n", "ab", LegacyPreset)).IsTrue();
        await Assert.That(LegacyPreset.BomHandling).IsEqualTo(SnapshotBomHandling.StripBom);
        await Assert.That(LegacyPreset.TrailingWhitespace).IsEqualTo(SnapshotTrailingWhitespace.Preserve);
    }

    private static SnapshotOptions LegacyPreset { get; } = SnapshotOptions.Default with
    {
        LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings,
        TrailingNewline = SnapshotTrailingNewline.Optional,
    };

    /// <summary>The written form keeps line breaks, joined with LF: a CRLF actual is written to the actual file as LF
    /// lines instead of one flattened line.</summary>
    [Test]
    public async Task WrittenActual_KeepsLineBreaksJoinedWithLf(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync("other\n", cancellationToken).ConfigureAwait(false);
        var options = SnapshotOptions.Default with { LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings };

        var result = await SnapshotEvaluator.EvaluateAsync("a\r\nb\n\nc\r\n", paths, options,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Mismatched);
        var written = await File.ReadAllTextAsync(paths.ActualFilePath, cancellationToken).ConfigureAwait(false);
        await Assert.That(written).IsEqualTo("a\nb\n\nc\n");
    }

    /// <summary>The comparison form is unchanged: an old baseline accepted with the line breaks removed (one line) still
    /// matches a multi-line actual.</summary>
    [Test]
    public async Task OldOneLineBaseline_StillMatchesMultiLineActual(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync("abc\n", cancellationToken).ConfigureAwait(false);
        var options = SnapshotOptions.Default with { LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings };

        var result = await SnapshotEvaluator.EvaluateAsync("a\nb\r\nc\n", paths, options,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Matched);
    }

    /// <summary>An accepted baseline keeps the line breaks (LF) and matches the same actual on the next run.</summary>
    [Test]
    public async Task AcceptedBaseline_KeepsLineBreaksAndRematches(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync("other\n", cancellationToken).ConfigureAwait(false);
        var options = SnapshotOptions.Default with { LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings };

        var accepted = await SnapshotEvaluator.EvaluateAsync("a\r\nb\n", paths, options,
            acceptModeOverride: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        await Assert.That(accepted.Outcome).IsEqualTo(SnapshotMatchOutcome.Accepted);
        var baseline = await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken).ConfigureAwait(false);
        await Assert.That(baseline).IsEqualTo("a\nb\n");

        var rerun = await SnapshotEvaluator.EvaluateAsync("a\r\nb\n", paths, options,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        await Assert.That(rerun.Outcome).IsEqualTo(SnapshotMatchOutcome.Matched);
    }

    /// <summary>The mismatch diff is rendered from the written forms, so it shows the real lines of the baseline and the
    /// actual instead of one flattened line each.</summary>
    [Test]
    public async Task MismatchDiff_ShowsLinesOfBothSides(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync("a\nx\n", cancellationToken).ConfigureAwait(false);
        var options = SnapshotOptions.Default with { LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings };

        var result = await SnapshotEvaluator.EvaluateAsync("a\ny\n", paths, options,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Diff).Contains("x");
        await Assert.That(result.Diff).Contains("y");
        await Assert.That(result.Diff).DoesNotContain("axy");
    }

    private static async Task<(SnapshotPaths Paths, string Directory)> CreateScenarioAsync(
        string expectedContent,
        CancellationToken cancellationToken)
    {
        var dir = Path.Combine(Path.GetTempPath(), "snapshot-ignore-line-endings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var paths = new SnapshotPaths(Path.Combine(dir, "case.expected.txt"), Path.Combine(dir, "case.actual.txt"));
        await File.WriteAllTextAsync(paths.ExpectedFilePath, expectedContent, cancellationToken).ConfigureAwait(false);
        return (paths, dir);
    }
}
