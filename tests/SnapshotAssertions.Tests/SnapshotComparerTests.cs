using System;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the option-driven normalisation logic in <see cref="SnapshotComparer"/>: line-ending
/// modes, BOM handling, trailing-whitespace policy, trailing-newline policy.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotComparerTests
{
    private static readonly string Bom = char.ConvertFromUtf32(0xFEFF);

    /// <summary>Strict default options: byte-for-byte equal strings match.</summary>
    [Test]
    public async Task DefaultOptions_ExactByteMatch_ReturnsTrue(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotComparer.AreEqual("foo", "foo", SnapshotOptions.Default)).IsTrue();
    }

    /// <summary>Strict default options: differing single character fails the match.</summary>
    [Test]
    public async Task DefaultOptions_DifferingByOneChar_ReturnsFalse(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotComparer.AreEqual("foo", "bar", SnapshotOptions.Default)).IsFalse();
    }

    /// <summary>NormalizeToLF: an LF and CRLF version of the same content match.</summary>
    [Test]
    public async Task NormalizeToLF_LfVsCrlf_Match(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with { LineEndingMode = SnapshotLineEndingMode.NormalizeToLF };
        await Assert.That(SnapshotComparer.AreEqual("a\r\nb\r\n", "a\nb\n", options)).IsTrue();
    }

    /// <summary>IgnoreLineEndings: line content compared, separators dropped.</summary>
    [Test]
    public async Task IgnoreLineEndings_DifferentSeparators_Match(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.NormalizedLineEndings;
        await Assert.That(SnapshotComparer.AreEqual("a\r\nb\nc\r", "abc", options)).IsTrue();
    }

    /// <summary>StripBom (default): a UTF-8 BOM on one side is ignored.</summary>
    [Test]
    public async Task StripBom_BomVsNoBom_Match(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotComparer.AreEqual(Bom + "hello\n", "hello\n", SnapshotOptions.Default)).IsTrue();
    }

    /// <summary>PreserveBom: a UTF-8 BOM on one side fails the match.</summary>
    [Test]
    public async Task PreserveBom_BomVsNoBom_Mismatch(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with { BomHandling = SnapshotBomHandling.PreserveBom };
        await Assert.That(SnapshotComparer.AreEqual(Bom + "hello\n", "hello\n", options)).IsFalse();
    }

    /// <summary>TrimTrailingPerLine: trailing whitespace differences are ignored.</summary>
    [Test]
    public async Task TrimTrailingPerLine_TrailingWhitespace_Match(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with { TrailingWhitespace = SnapshotTrailingWhitespace.TrimTrailingPerLine };
        await Assert.That(SnapshotComparer.AreEqual("foo   \nbar  \n", "foo\nbar\n", options)).IsTrue();
    }

    /// <summary>Required trailing newline: a file without one does not match a file with one.</summary>
    [Test]
    public async Task RequiredTrailingNewline_MissingTrailing_Mismatch(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with { LineEndingMode = SnapshotLineEndingMode.NormalizeToLF };
        await Assert.That(SnapshotComparer.AreEqual("foo", "foo\n", options)).IsFalse();
    }

    /// <summary>Optional trailing newline: presence does not affect match outcome.</summary>
    [Test]
    public async Task OptionalTrailingNewline_PresentOrAbsent_Match(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with
        {
            LineEndingMode = SnapshotLineEndingMode.NormalizeToLF,
            TrailingNewline = SnapshotTrailingNewline.Optional,
        };
        await Assert.That(SnapshotComparer.AreEqual("foo", "foo\n", options)).IsTrue();
    }

    /// <summary>Null arguments throw <see cref="ArgumentNullException"/>.</summary>
    [Test]
    public void NullArguments_Throw(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentNullException>(() => SnapshotComparer.AreEqual(null!, "x", SnapshotOptions.Default));
        Assert.Throws<ArgumentNullException>(() => SnapshotComparer.AreEqual("x", null!, SnapshotOptions.Default));
        Assert.Throws<ArgumentNullException>(() => SnapshotComparer.AreEqual("x", "x", null!));
    }
}
