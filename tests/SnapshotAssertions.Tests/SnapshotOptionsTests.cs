using System;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the strict-default behavior of <see cref="SnapshotOptions"/> and the
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

    /// <summary>The <see cref="SnapshotOptions.NormalizedLineEndings"/> preset relaxes both
    /// line-ending mode (line breaks stripped) and trailing-newline policy (presence vs
    /// absence unobservable). BOM and per-line trailing-whitespace handling stay strict.
    /// The two relaxations go together: under <see cref="SnapshotLineEndingMode.IgnoreLineEndings"/>
    /// the trailing newline is just another stripped line break, so any consumer reaching
    /// for "ignore line endings" expects the trailing one to be ignored too.</summary>
    [Test]
    public async Task NormalizedLineEndingsPresetRelaxesLineEndingsAndTrailingNewline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.NormalizedLineEndings;

        await Assert.That(options.LineEndingMode).IsEqualTo(SnapshotLineEndingMode.IgnoreLineEndings);
        await Assert.That(options.TrailingNewline).IsEqualTo(SnapshotTrailingNewline.Optional);
        await Assert.That(options.BomHandling).IsEqualTo(SnapshotBomHandling.StripBom);
        await Assert.That(options.TrailingWhitespace).IsEqualTo(SnapshotTrailingWhitespace.Preserve);
    }

    /// <summary>The default options carry no custom normalizer.</summary>
    [Test]
    public async Task DefaultOptions_NormalizerIsNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotOptions.Default.Normalizer).IsNull();
    }

    /// <summary>A null normalizer is rejected.</summary>
    [Test]
    public async Task WithNormalizer_Null_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SnapshotOptions.Default.WithNormalizer(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>A single normalizer is stored and applied.</summary>
    [Test]
    public async Task WithNormalizer_StoresTheTransform(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default.WithNormalizer(value => value.Trim());

        await Assert.That(options.Normalizer).IsNotNull();
        await Assert.That(options.Normalizer!("  x  ")).IsEqualTo("x");
    }

    /// <summary>Chained normalizers compose in registration order: the first registered runs first.</summary>
    [Test]
    public async Task WithNormalizer_Composes_InRegistrationOrder(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default
            .WithNormalizer(value => value + "1")
            .WithNormalizer(value => value + "2");

        await Assert.That(options.Normalizer!("x")).IsEqualTo("x12");
    }
}
