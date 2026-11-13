using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Several evaluations of one snapshot at the same time, the way they meet in practice: the test processes of two
/// target frameworks accepting the same project baseline in one <c>dotnet test</c> run, and tests that share a default
/// name (two generic instantiations of one class, or equal class names in two namespaces) running in parallel. On
/// Windows a replace-move or delete of a file that another evaluation is reading or replacing fails transiently, and
/// the evaluator must ride that out instead of failing the test.
/// </summary>
[Category("Smoke")]
[Timeout(60_000)]
internal sealed class SnapshotEvaluatorConcurrencyTests
{
    private const int Writers = 4;
    private const int Rounds = 50;

    /// <summary>Concurrent accepts of the same content into one baseline all complete, and the baseline holds the content.</summary>
    [Test]
    public async Task ConcurrentAccept_SameBaseline_AllComplete(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByName(directory, "accepted");

            await RunConcurrentlyAsync(
                () => SnapshotEvaluator.EvaluateAsync("content\n", paths, SnapshotOptions.Default, acceptModeOverride: true, cancellationToken: cancellationToken),
                rounds: Rounds,
                cancellationToken);

            await Assert.That(await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken)).IsEqualTo("content\n");
            await Assert.That(Directory.GetFiles(directory)).HasSingleItem();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Concurrent mismatches against one baseline all report the mismatch while rewriting the same actual file.</summary>
    [Test]
    public async Task ConcurrentMismatch_SameActualFile_AllReportTheMismatch(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByName(directory, "mismatched");
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "other\n", cancellationToken);

            await RunConcurrentlyAsync(
                async () =>
                {
                    var result = await SnapshotEvaluator.EvaluateAsync("content\n", paths, SnapshotOptions.Default, acceptModeOverride: false, cancellationToken: cancellationToken);
                    await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Mismatched);
                },
                rounds: Rounds,
                cancellationToken);

            await Assert.That(await File.ReadAllTextAsync(paths.ActualFilePath, cancellationToken)).IsEqualTo("content\n");
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Matching evaluations that delete a stale actual file at the same time all report the match.</summary>
    [Test]
    public async Task ConcurrentMatch_StaleActualFile_AllReportTheMatch(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByName(directory, "matched");
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "content\n", cancellationToken);

            for (var round = 0; round < Rounds; round++)
            {
                await File.WriteAllTextAsync(paths.ActualFilePath, "stale\n", cancellationToken);
                await RunConcurrentlyAsync(
                    async () =>
                    {
                        var result = await SnapshotEvaluator.EvaluateAsync("content\n", paths, SnapshotOptions.Default, acceptModeOverride: false, cancellationToken: cancellationToken);
                        await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Matched);
                    },
                    rounds: 1,
                    cancellationToken);
                await Assert.That(File.Exists(paths.ActualFilePath)).IsFalse();
            }

            await Assert.That(File.Exists(paths.ExpectedFilePath)).IsTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A baseline that is read-only fails the accept with the path in the message after a short bounded wait, not after a long one. Windows
    /// only: elsewhere a read-only file can be replaced by a move, so there is nothing to refuse.</summary>
    [Test]
    public async Task Accept_ReadOnlyBaseline_FailsPromptlyNamingThePath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Skip.Unless(OperatingSystem.IsWindows(), "A read-only baseline blocks a replace-move on Windows only.");
        var directory = CreateTempDirectory();
        var paths = SnapshotFileResolver.ResolveByName(directory, "readonly");
        try
        {
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "old\n", cancellationToken);
            File.SetAttributes(paths.ExpectedFilePath, FileAttributes.ReadOnly);
            var clock = System.Diagnostics.Stopwatch.StartNew();

            var exception = await Assert.That(async () => await SnapshotEvaluator.EvaluateAsync("new\n", paths, SnapshotOptions.Default, acceptModeOverride: true, cancellationToken: cancellationToken))
                .Throws<UnauthorizedAccessException>();

            await Assert.That(exception!.Message).Contains(paths.ExpectedFilePath);
            await Assert.That(clock.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
            await Assert.That(await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken)).IsEqualTo("old\n");
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(1);
        }
        finally
        {
            File.SetAttributes(paths.ExpectedFilePath, FileAttributes.Normal);
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A replace-move that is blocked for a moment (another evaluation holds the baseline open without allowing a
    /// delete) waits and succeeds once the holder lets go. On Windows the first attempts fail; elsewhere nothing blocks
    /// and the accept just succeeds.
    /// </summary>
    [Test]
    public async Task Accept_BaselineHeldOpenBriefly_SucceedsWhenReleased(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByName(directory, "held");
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "old\n", cancellationToken);

            var holder = new FileStream(paths.ExpectedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var evaluation = SnapshotEvaluator.EvaluateAsync("new\n", paths, SnapshotOptions.Default, acceptModeOverride: true, cancellationToken: cancellationToken);
            await Task.Delay(150, cancellationToken);
            await holder.DisposeAsync();

            var result = await evaluation;

            await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Accepted);
            await Assert.That(await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken)).IsEqualTo("new\n");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The same for the delete of a stale actual file after a match: it waits for the holder and then succeeds.</summary>
    [Test]
    public async Task Match_StaleActualFileHeldOpenBriefly_SucceedsWhenReleased(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByName(directory, "stale");
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "same\n", cancellationToken);
            await File.WriteAllTextAsync(paths.ActualFilePath, "stale\n", cancellationToken);

            var holder = new FileStream(paths.ActualFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var evaluation = SnapshotEvaluator.EvaluateAsync("same\n", paths, SnapshotOptions.Default, acceptModeOverride: false, cancellationToken: cancellationToken);
            await Task.Delay(150, cancellationToken);
            await holder.DisposeAsync();

            var result = await evaluation;

            await Assert.That(result.Outcome).IsEqualTo(SnapshotMatchOutcome.Matched);
            await Assert.That(File.Exists(paths.ActualFilePath)).IsFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A stale actual file that stays open exclusively for the whole evaluation outlasts the attempts: the delete fails
    /// with an <see cref="IOException"/> that names the file. Windows only: elsewhere an open file can be deleted.
    /// </summary>
    [Test]
    public async Task Match_StaleActualFileHeldOpenExclusively_FailsNamingThePath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Skip.Unless(OperatingSystem.IsWindows(), "An open file blocks a delete on Windows only.");
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByName(directory, "locked");
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "same\n", cancellationToken);
            await File.WriteAllTextAsync(paths.ActualFilePath, "stale\n", cancellationToken);
            var holder = new FileStream(paths.ActualFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            await using (holder)
            {
                var exception = await Assert.That(async () => await SnapshotEvaluator.EvaluateAsync("same\n", paths, SnapshotOptions.Default, acceptModeOverride: false, cancellationToken: cancellationToken))
                    .Throws<IOException>();

                await Assert.That(exception!.Message).Contains(paths.ActualFilePath);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task RunConcurrentlyAsync(Func<Task> operation, int rounds, CancellationToken cancellationToken)
    {
        var tasks = Enumerable.Range(0, Writers)
            .Select(_ => Task.Run(
                async () =>
                {
                    for (var round = 0; round < rounds; round++)
                        await operation().ConfigureAwait(false);
                },
                cancellationToken))
            .ToArray();
        await Task.WhenAll(tasks);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "evaluator-concurrency-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
