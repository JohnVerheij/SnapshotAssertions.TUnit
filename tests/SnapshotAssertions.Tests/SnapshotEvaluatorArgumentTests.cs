using System;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// The argument checks of <see cref="SnapshotEvaluator.EvaluateAsync"/> for a <see cref="SnapshotPaths"/> that was
/// built by hand with a missing path: a struct cannot itself be null, but its strings can, and the documented
/// <see cref="ArgumentNullException"/> must come from the evaluator, not a later <see cref="NullReferenceException"/>.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotEvaluatorArgumentTests
{
    /// <summary>A null expected path is rejected before any file is touched.</summary>
    [Test]
    public async Task NullExpectedPath_ThrowsArgumentNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paths = new SnapshotPaths(null!, "actual.txt");

        var exception = await Assert.That(async () => await SnapshotEvaluator.EvaluateAsync("x", paths, SnapshotOptions.Default, cancellationToken: cancellationToken))
            .Throws<ArgumentNullException>();

        await Assert.That(exception!.Message).Contains("paths.ExpectedFilePath must not be null.");
    }

    /// <summary>A null actual path is rejected before any file is touched.</summary>
    [Test]
    public async Task NullActualPath_ThrowsArgumentNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paths = new SnapshotPaths("expected.txt", null!);

        var exception = await Assert.That(async () => await SnapshotEvaluator.EvaluateAsync("x", paths, SnapshotOptions.Default, cancellationToken: cancellationToken))
            .Throws<ArgumentNullException>();

        await Assert.That(exception!.Message).Contains("paths.ActualFilePath must not be null.");
    }
}
