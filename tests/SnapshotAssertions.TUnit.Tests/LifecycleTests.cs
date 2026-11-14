using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;
using TUnit.Core.Interfaces;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Calls the snapshot assertion from the places a test class has besides the test body: the constructor,
/// <c>[Before(Test)]</c> and <c>[After(Test)]</c> hooks, a class fixture, a retried test and a repeated test. The
/// default name comes from the running test, so every one of these places must see the name of the test it serves.
/// </summary>
[Timeout(30_000)]
internal sealed class LifecycleTests : IAsyncInitializer
{
    private static readonly string Directory_ = CreateDirectory();

    private readonly string _constructorName = ResolveDefaultName();
    private string? _beforeName;
    private string? _initializerName;

    /// <summary>Records the default name a hook resolves, to compare with the test's own.</summary>
    [Before(Test)]
    public void RecordBeforeName(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _beforeName = ResolveDefaultName();
    }

    /// <summary>Resolves the default name from an after hook and checks it is the test's.</summary>
    [After(Test)]
    public async Task CheckAfterName(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var afterName = ResolveDefaultName();
        await Assert.That(afterName).IsEqualTo(Path.GetFileName(SnapshotAssertionImpl.ResolvePaths(null, null, acceptModeOverride: false).ExpectedFilePath));
        await Assert.That(afterName).StartsWith(nameof(LifecycleTests) + ".");
    }

    /// <summary>The constructor, the before hook and the test body resolve the same default name, which is the test's.</summary>
    [Test]
    public async Task DefaultName_IsTheSameInConstructorHookAndBody(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = nameof(LifecycleTests) + "." + nameof(DefaultName_IsTheSameInConstructorHookAndBody) + SnapshotFileResolver.ExpectedExtension;

        await Assert.That(_constructorName).IsEqualTo(expected);
        await Assert.That(_beforeName).IsEqualTo(expected);
        await Assert.That(ResolveDefaultName()).IsEqualTo(expected);
    }

    /// <summary>
    /// Work started from the test body on the thread pool keeps the test context (it flows with the execution context), so
    /// a snapshot taken there resolves the name of the test that started it. Work that suppresses the flow gets the
    /// documented "no active test context" error instead (see the implementation tests).
    /// </summary>
    [Test]
    public async Task BackgroundTask_ResolvesTheNameOfTheTestThatStartedIt(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = nameof(LifecycleTests) + "." + nameof(BackgroundTask_ResolvesTheNameOfTheTestThatStartedIt) + SnapshotFileResolver.ExpectedExtension;

        var name = await Task.Run(ResolveDefaultName, cancellationToken);

        await Assert.That(name).IsEqualTo(expected);
    }

    /// <summary>
    /// A retried test that fails first and passes second: the second attempt matches and the actual file the first
    /// attempt wrote is gone afterwards, so a passing test leaves nothing behind.
    /// </summary>
    [Test]
    [Retry(2)]
    public async Task Retry_PassingAttemptRemovesTheActualFileOfTheFailedOne(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paths = SnapshotFileResolver.ResolveByName(Directory_, nameof(Retry_PassingAttemptRemovesTheActualFileOfTheFailedOne));
        var attempt = TestContext.Current!.Execution.CurrentRetryAttempt;
        await File.WriteAllTextAsync(paths.ExpectedFilePath, "second\n", cancellationToken);

        if (attempt > 0)
            await Assert.That(File.Exists(paths.ActualFilePath)).IsTrue();

        await Assert.That(attempt == 0 ? "first\n" : "second\n").MatchesSnapshot().AtPath(paths.ExpectedFilePath);

        await Assert.That(File.Exists(paths.ActualFilePath)).IsFalse();
    }

    /// <summary>Repeated runs of one test all match the same baseline and leave no actual file.</summary>
    [Test]
    [Repeat(3)]
    public async Task Repeat_AllRunsMatchTheSameBaseline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paths = SnapshotFileResolver.ResolveByName(Directory_, nameof(Repeat_AllRunsMatchTheSameBaseline));

        await Assert.That("repeated\n").MatchesSnapshot().AtPath(paths.ExpectedFilePath);

        await Assert.That(File.Exists(paths.ActualFilePath)).IsFalse();
    }

    /// <summary>
    /// The initializer of the test class (<c>IAsyncInitializer</c>, the hook a class fixture uses) resolves a default
    /// name while the instance initializes. It runs for a test, so it sees that test's name.
    /// </summary>
    [Test]
    public async Task Initializer_ResolvesTheNameOfTheTestItServes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = nameof(LifecycleTests) + "." + nameof(Initializer_ResolvesTheNameOfTheTestItServes) + SnapshotFileResolver.ExpectedExtension;

        await Assert.That(_initializerName).IsEqualTo(expected);
    }

    /// <inheritdoc/>
    public Task InitializeAsync()
    {
        _initializerName = ResolveDefaultName();
        return Task.CompletedTask;
    }

    /// <summary>Resolves the default expected file name for the running test and returns the file name only.</summary>
    internal static string ResolveDefaultName()
        => Path.GetFileName(SnapshotAssertionImpl.ResolvePaths(null, null, acceptModeOverride: false).ExpectedFilePath);

    /// <summary>Removes the directory that holds the baselines of this class once all its tests have run.</summary>
    [After(Class)]
    public static void DeleteDirectory(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (System.IO.Directory.Exists(Directory_))
            System.IO.Directory.Delete(Directory_, recursive: true);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lifecycle-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);

        // Written once, here: repeated runs execute in parallel and must only read their baseline.
        File.WriteAllText(SnapshotFileResolver.ResolveByName(directory, nameof(Repeat_AllRunsMatchTheSameBaseline)).ExpectedFilePath, "repeated" + (char)10);
        return directory;
    }
}
