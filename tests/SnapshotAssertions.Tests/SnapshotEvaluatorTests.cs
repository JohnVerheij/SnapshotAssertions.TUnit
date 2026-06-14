using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the IO-orchestration behavior of <see cref="SnapshotEvaluator"/>: matched, mismatched,
/// no-baseline, and accept-mode flows. Each test uses a fresh temp directory so runs do not
/// interfere with each other.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SnapshotEvaluatorTests
{
    /// <summary>Matching content + existing baseline produces a Matched outcome and removes
    /// any stale .actual.txt sibling.</summary>
    [Test]
    public async Task MatchingContent_ReturnsMatched(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync("hello\n", expectedContent: "hello\n", cancellationToken).ConfigureAwait(false);

        var result = await SnapshotEvaluator.EvaluateAsync("hello\n", paths, SnapshotOptions.Default,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Matched);
        await Assert.That(result.IsPass).IsTrue();
        await Assert.That(File.Exists(paths.ActualFilePath)).IsFalse();
    }

    /// <summary>Mismatched content produces a Mismatched outcome with the actual file
    /// written and a non-empty diff.</summary>
    [Test]
    public async Task MismatchedContent_WritesActualAndReturnsMismatched(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync(actualContent: "world\n", expectedContent: "hello\n", cancellationToken).ConfigureAwait(false);

        var result = await SnapshotEvaluator.EvaluateAsync("world\n", paths, SnapshotOptions.Default,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Mismatched);
        await Assert.That(result.IsPass).IsFalse();
        await Assert.That(File.Exists(paths.ActualFilePath)).IsTrue();
        await Assert.That(result.Diff).IsNotNull();
    }

    /// <summary>No expected file produces a NoBaseline outcome with the actual file written.</summary>
    [Test]
    public async Task NoBaseline_WritesActualAndReturnsNoBaseline(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var paths = new SnapshotPaths(
            Path.Combine(dir, "missing.expected.txt"),
            Path.Combine(dir, "missing.actual.txt"));

        var result = await SnapshotEvaluator.EvaluateAsync("anything\n", paths, SnapshotOptions.Default,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.NoBaseline);
        await Assert.That(result.IsPass).IsFalse();
        await Assert.That(File.Exists(paths.ActualFilePath)).IsTrue();
    }

    /// <summary>Accept-mode + mismatch: actual content is written over the expected file and
    /// the outcome is Accepted.</summary>
    [Test]
    public async Task AcceptMode_OverwritesBaselineOnMismatch(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync(actualContent: "world\n", expectedContent: "hello\n", cancellationToken).ConfigureAwait(false);

        var result = await SnapshotEvaluator.EvaluateAsync("world\n", paths, SnapshotOptions.Default,
            acceptModeOverride: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Accepted);
        await Assert.That(result.IsPass).IsTrue();
        var newExpected = await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken).ConfigureAwait(false);
        await Assert.That(newExpected).IsEqualTo("world\n");
    }

    /// <summary>Accept-mode + no baseline: actual content is written as the new expected file
    /// and the outcome is Accepted.</summary>
    [Test]
    public async Task AcceptMode_BootstrapsMissingBaseline(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var paths = new SnapshotPaths(
            Path.Combine(dir, "bootstrap.expected.txt"),
            Path.Combine(dir, "bootstrap.actual.txt"));

        var result = await SnapshotEvaluator.EvaluateAsync("first\n", paths, SnapshotOptions.Default,
            acceptModeOverride: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Accepted);
        var content = await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken).ConfigureAwait(false);
        await Assert.That(content).IsEqualTo("first\n");
    }

    /// <summary>No baseline + accept-mode: the written expected baseline is the normalizer's
    /// output, not the raw subject, so an accepted first-run baseline is already canonical.</summary>
    [Test]
    public async Task AcceptMode_BootstrapsMissingBaseline_WritesNormalizedForm(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var paths = new SnapshotPaths(
            Path.Combine(dir, "normalized.expected.txt"),
            Path.Combine(dir, "normalized.actual.txt"));

        // A normalizer that masks a volatile token. A raw write would persist the volatile
        // input verbatim; the normalized write must persist the masked output instead.
        var options = SnapshotOptions.Default.WithNormalizer(
            text => text.Replace("id=42", "id=<scrubbed>", StringComparison.Ordinal));

        var result = await SnapshotEvaluator.EvaluateAsync("id=42\n", paths, options,
            acceptModeOverride: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Accepted);
        var written = await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken).ConfigureAwait(false);
        await Assert.That(written).IsEqualTo("id=<scrubbed>\n");
        await Assert.That(written).DoesNotContain("id=42");
    }

    /// <summary>No baseline (non-accept): the written .actual candidate a consumer renames to
    /// accept is the normalized form, so renaming it commits canonical content.</summary>
    [Test]
    public async Task NoBaseline_WritesNormalizedActualCandidate(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var paths = new SnapshotPaths(
            Path.Combine(dir, "candidate.expected.txt"),
            Path.Combine(dir, "candidate.actual.txt"));

        var options = SnapshotOptions.Default.WithNormalizer(
            text => text.Replace("VOLATILE", "STABLE", StringComparison.Ordinal));

        var result = await SnapshotEvaluator.EvaluateAsync("value=VOLATILE\n", paths, options,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.NoBaseline);
        var candidate = await File.ReadAllTextAsync(paths.ActualFilePath, cancellationToken).ConfigureAwait(false);
        await Assert.That(candidate).IsEqualTo("value=STABLE\n");
    }

    /// <summary>Accept-mode over an existing mismatching baseline writes the normalized subject,
    /// keeping the persisted baseline consistent with what the next comparison reads back.</summary>
    [Test]
    public async Task AcceptMode_OverwriteMismatch_WritesNormalizedForm(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync(actualContent: "id=99\n", expectedContent: "old\n", cancellationToken).ConfigureAwait(false);

        var options = SnapshotOptions.Default.WithNormalizer(
            text => text.Replace("id=99", "id=<scrubbed>", StringComparison.Ordinal));

        var result = await SnapshotEvaluator.EvaluateAsync("id=99\n", paths, options,
            acceptModeOverride: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Accepted);
        var written = await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken).ConfigureAwait(false);
        await Assert.That(written).IsEqualTo("id=<scrubbed>\n");
    }

    /// <summary>Mismatch (non-accept): the written .actual candidate is the normalized form, not
    /// the raw subject, so a consumer who renames it to accept commits canonical content. Pins the
    /// mismatch-branch write content, not just that a .actual file appears.</summary>
    [Test]
    public async Task MismatchedContent_WritesNormalizedActualCandidate(CancellationToken cancellationToken)
    {
        // The baseline differs from the normalized actual, so the comparison mismatches and the
        // non-accept branch writes the .actual candidate.
        var (paths, _) = await CreateScenarioAsync(
            actualContent: "value=VOLATILE\n", expectedContent: "value=STABLE-OTHER\n", cancellationToken).ConfigureAwait(false);

        var options = SnapshotOptions.Default.WithNormalizer(
            text => text.Replace("VOLATILE", "STABLE", StringComparison.Ordinal));

        var result = await SnapshotEvaluator.EvaluateAsync("value=VOLATILE\n", paths, options,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Mismatched);
        var candidate = await File.ReadAllTextAsync(paths.ActualFilePath, cancellationToken).ConfigureAwait(false);
        await Assert.That(candidate).IsEqualTo("value=STABLE\n");
        await Assert.That(candidate).DoesNotContain("VOLATILE");
    }

    /// <summary>A matching run deletes a stale <c>.actual.txt</c> left from a previous mismatch.</summary>
    [Test]
    public async Task MatchingContent_DeletesStaleActualFile(CancellationToken cancellationToken)
    {
        var (paths, _) = await CreateScenarioAsync("hello\n", expectedContent: "hello\n", cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(paths.ActualFilePath, "stale\n", cancellationToken).ConfigureAwait(false);

        var result = await SnapshotEvaluator.EvaluateAsync("hello\n", paths, SnapshotOptions.Default,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Matched);
        await Assert.That(File.Exists(paths.ActualFilePath)).IsFalse();
    }

    /// <summary>A no-baseline write produces the actual file with the normalized content and leaves no
    /// temporary file behind: the atomic temp-then-move write cleans up its temp on success.</summary>
    [Test]
    public async Task NoBaseline_WritesActualAtomically_NoTempLeftBehind(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var paths = new SnapshotPaths(
            Path.Combine(dir, "atomic.expected.txt"),
            Path.Combine(dir, "atomic.actual.txt"));

        var result = await SnapshotEvaluator.EvaluateAsync("payload\n", paths, SnapshotOptions.Default,
            acceptModeOverride: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.NoBaseline);
        await Assert.That(await File.ReadAllTextAsync(paths.ActualFilePath, cancellationToken).ConfigureAwait(false))
            .IsEqualTo("payload\n");
        await Assert.That(Directory.GetFiles(dir, "*.tmp")).IsEmpty();
    }

    /// <summary>Concurrent evaluations writing distinct actual files all succeed without a sharing
    /// violation, exercising the atomic write under parallelism.</summary>
    [Test]
    public async Task ConcurrentEvaluations_DistinctPaths_AllSucceed(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var tasks = new Task<SnapshotResult>[16];
        for (var i = 0; i < tasks.Length; i++)
        {
            var paths = new SnapshotPaths(
                Path.Combine(dir, $"c{i}.expected.txt"),
                Path.Combine(dir, $"c{i}.actual.txt"));
            tasks[i] = SnapshotEvaluator.EvaluateAsync($"item-{i}\n", paths, SnapshotOptions.Default,
                acceptModeOverride: false, cancellationToken: cancellationToken);
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(results.Length).IsEqualTo(16);
        await Assert.That(Directory.GetFiles(dir, "*.tmp")).IsEmpty();
    }

    /// <summary>A failed write cleans up its temp: with the move inside the try/finally, a write that
    /// fails (here because the target path is an existing directory, so the move cannot complete) leaves
    /// no temporary file behind.</summary>
    [Test]
    public async Task FailedWrite_CleansUpTempFile(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        // Make the actual path an existing directory so the temp-then-move write fails at the move.
        var actualPath = Path.Combine(dir, "actual-as-directory");
        Directory.CreateDirectory(actualPath);
        var paths = new SnapshotPaths(Path.Combine(dir, "x.expected.txt"), actualPath);

        // The move onto a directory throws UnauthorizedAccessException on Windows and IOException on
        // Linux, both SystemException; the assertion that matters is that the temp is cleaned up.
        await Assert.That(async () => await SnapshotEvaluator.EvaluateAsync("payload\n", paths,
                SnapshotOptions.Default, acceptModeOverride: false, cancellationToken: cancellationToken))
            .Throws<SystemException>();

        await Assert.That(Directory.GetFiles(dir, "*.tmp")).IsEmpty();
    }

    private static async Task<(SnapshotPaths Paths, string Directory)> CreateScenarioAsync(
        string actualContent,
        string expectedContent,
        CancellationToken cancellationToken)
    {
        _ = actualContent;
        var dir = CreateTempDirectory();
        var expectedPath = Path.Combine(dir, "case.expected.txt");
        var actualPath = Path.Combine(dir, "case.actual.txt");
        await File.WriteAllTextAsync(expectedPath, expectedContent, cancellationToken).ConfigureAwait(false);
        return (new SnapshotPaths(expectedPath, actualPath), dir);
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "snapshot-evaluator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
