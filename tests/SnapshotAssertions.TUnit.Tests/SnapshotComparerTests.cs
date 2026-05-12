using System;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the option-driven normalization logic in <see cref="SnapshotComparer"/>: line-ending
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

    /// <summary>Content ending with a bare CR (no LF) is detected as having a trailing newline.
    /// Pins the third short-circuit branch in the <c>content[^1] == '\r'</c> check inside
    /// <c>NormalizeLineByLine</c>, which the standard \n / \r\n test inputs do not exercise.</summary>
    [Test]
    public async Task NormalizedLineEndings_BareCarriageReturnTerminator_TreatedAsTrailingNewline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lhs = "alpha\rbeta\r";
        var rhs = "alpha\nbeta\n";
        await Assert.That(SnapshotComparer.AreEqual(lhs, rhs, SnapshotOptions.NormalizedLineEndings)).IsTrue();
    }

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

    /// <summary>An invalid enum value for <c>TrailingNewline</c> falls through the switch
    /// statement to the default branch, throwing <see cref="ArgumentOutOfRangeException"/>.
    /// Covers the defensive default arm.</summary>
    [Test]
    public async Task InvalidTrailingNewline_ThrowsArgumentOutOfRange(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bogusOptions = SnapshotOptions.Default with
        {
            LineEndingMode = SnapshotLineEndingMode.NormalizeToLF,
            TrailingNewline = (SnapshotTrailingNewline)999,
        };
        await Assert.That(() => SnapshotComparer.AreEqual("foo\n", "foo\n", bogusOptions))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Forbidden trailing newline: same equality outcome as Optional today (both
    /// strip trailing newlines before comparison so presence vs absence is unobservable). The
    /// test pins the current behaviour and exercises the <c>Forbidden</c> branch of
    /// <see cref="SnapshotComparer.Normalize"/>'s <c>ApplyTrailingNewlinePolicy</c> helper.</summary>
    [Test]
    public async Task ForbiddenTrailingNewline_PresentOrAbsent_Match(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with
        {
            LineEndingMode = SnapshotLineEndingMode.NormalizeToLF,
            TrailingNewline = SnapshotTrailingNewline.Forbidden,
        };
        await Assert.That(SnapshotComparer.AreEqual("foo", "foo\n", options)).IsTrue();
        await Assert.That(SnapshotComparer.AreEqual("foo\n", "foo", options)).IsTrue();
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
