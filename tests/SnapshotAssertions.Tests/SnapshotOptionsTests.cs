using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the strict-default behaviour of <see cref="SnapshotOptions"/> and the
/// <see cref="SnapshotOptions.NormalizedLineEndings"/> preset. Both are part of the
/// stable surface; tests fail if either drifts.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotOptionsTests
{
    /// <summary>The strict-default options preserve everything as-is.</summary>
    [Test]
    public async Task DefaultOptionsAreStrict(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default;

        await Assert.That(options.LineEndingMode).IsEqualTo(SnapshotLineEndingMode.Ordinal);
        await Assert.That(options.BomHandling).IsEqualTo(SnapshotBomHandling.StripBom);
        await Assert.That(options.TrailingWhitespace).IsEqualTo(SnapshotTrailingWhitespace.Preserve);
        await Assert.That(options.TrailingNewline).IsEqualTo(SnapshotTrailingNewline.Required);
    }

    /// <summary>The <see cref="SnapshotOptions.NormalizedLineEndings"/> preset relaxes only
    /// the line-ending mode; every other property keeps the strict default.</summary>
    [Test]
    public async Task NormalizedLineEndingsPresetRelaxesOnlyLineEndings(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.NormalizedLineEndings;

        await Assert.That(options.LineEndingMode).IsEqualTo(SnapshotLineEndingMode.IgnoreLineEndings);
        await Assert.That(options.BomHandling).IsEqualTo(SnapshotBomHandling.StripBom);
        await Assert.That(options.TrailingWhitespace).IsEqualTo(SnapshotTrailingWhitespace.Preserve);
        await Assert.That(options.TrailingNewline).IsEqualTo(SnapshotTrailingNewline.Required);
    }
}
