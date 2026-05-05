using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the rendering of <see cref="SnapshotResult.Describe"/> for all four
/// <see cref="SnapshotMatchOutcome"/> values. The exact text format is documented as not
/// stable, but each outcome's description must mention the relevant paths and outcome
/// name so a developer reading the failure message can act on it.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotResultDescribeTests
{
    /// <summary>Matched outcome's description mentions the expected path.</summary>
    [Test]
    public async Task Matched_DescribeMentionsExpectedPath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Matched("/tmp/foo.expected.txt");

        await Assert.That(result.Describe()).Contains("/tmp/foo.expected.txt");
        await Assert.That(result.IsPass).IsTrue();
    }

    /// <summary>Accepted outcome's description mentions accept-mode and the expected
    /// path.</summary>
    [Test]
    public async Task Accepted_DescribeMentionsAcceptModeAndPath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Accepted("/tmp/foo.expected.txt");

        var description = result.Describe();
        await Assert.That(description).Contains("SNAPSHOT_ACCEPT");
        await Assert.That(description).Contains("/tmp/foo.expected.txt");
        await Assert.That(result.IsPass).IsTrue();
    }

    /// <summary>NoBaseline outcome's description mentions both paths and the rename
    /// guidance.</summary>
    [Test]
    public async Task NoBaseline_DescribeMentionsBothPathsAndRenameGuidance(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.NoBaseline("/tmp/foo.expected.txt", "/tmp/foo.actual.txt");

        var description = result.Describe();
        await Assert.That(description).Contains("/tmp/foo.expected.txt");
        await Assert.That(description).Contains("/tmp/foo.actual.txt");
        await Assert.That(description).Contains("rename");
        await Assert.That(result.IsPass).IsFalse();
    }

    /// <summary>Mismatched outcome's description mentions both paths, the diff content, and
    /// the rename guidance.</summary>
    [Test]
    public async Task Mismatched_DescribeMentionsBothPathsAndDiff(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diff = "-old\n+new\n";
        var result = SnapshotResult.Mismatched("/tmp/foo.expected.txt", "/tmp/foo.actual.txt", diff);

        var description = result.Describe();
        await Assert.That(description).Contains("/tmp/foo.expected.txt");
        await Assert.That(description).Contains("/tmp/foo.actual.txt");
        await Assert.That(description).Contains("-old");
        await Assert.That(description).Contains("+new");
        await Assert.That(description).Contains("rename");
        await Assert.That(result.IsPass).IsFalse();
    }

    /// <summary>WriteDescription throws on null writer.</summary>
    [Test]
    public void WriteDescription_NullWriter_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Matched("/tmp/x.expected.txt");
        Assert.Throws<ArgumentNullException>(() => result.WriteDescription(null!));
    }

    /// <summary>WriteDescription writes the same content as <see cref="SnapshotResult.Describe"/>
    /// to the supplied <see cref="TextWriter"/>.</summary>
    [Test]
    public async Task WriteDescription_RoundTripsViaTextWriter(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.Matched("/tmp/x.expected.txt");

        using var writer = new StringWriter();
        result.WriteDescription(writer);

        await Assert.That(writer.ToString()).IsEqualTo(result.Describe());
    }
}
