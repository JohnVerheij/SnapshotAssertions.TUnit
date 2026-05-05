namespace SnapshotAssertions;

/// <summary>
/// Configuration for snapshot comparison: line-ending handling, BOM behaviour, trailing
/// whitespace, and trailing-newline policy. Strict by default; opt into normalisation
/// explicitly via the convenience presets or by mutating the properties directly.
/// </summary>
/// <remarks>
/// The default options preserve the captured artefact byte-for-byte. Cross-platform false
/// positives (LF vs CRLF, BOM presence) are real but should be opted into via
/// <see cref="NormalizedLineEndings"/> rather than silently normalised away. This matches
/// the family-wide convention of explicit <c>StringComparison</c> on string-matching APIs.
/// </remarks>
public sealed record SnapshotOptions
{
    /// <summary>
    /// Strict defaults: preserve everything as-is. Used when no options are passed to the
    /// snapshot assertion entry points.
    /// </summary>
    public static SnapshotOptions Default { get; } = new();

    /// <summary>
    /// Convenience preset for cross-platform tests that should treat LF/CRLF differences as
    /// non-meaningful. All other strict-default behaviours are retained.
    /// </summary>
    public static SnapshotOptions NormalizedLineEndings { get; } = new()
    {
        LineEndingMode = SnapshotLineEndingMode.IgnoreLineEndings,
    };

    /// <summary>How line-ending differences between the actual content and the expected
    /// baseline are handled. Defaults to <see cref="SnapshotLineEndingMode.Ordinal"/>
    /// (no normalisation).</summary>
    public SnapshotLineEndingMode LineEndingMode { get; init; } = SnapshotLineEndingMode.Ordinal;

    /// <summary>How a leading byte-order-mark in either file is handled. Defaults to
    /// <see cref="SnapshotBomHandling.StripBom"/>: a UTF-8 BOM is stripped from both sides
    /// before comparison.</summary>
    public SnapshotBomHandling BomHandling { get; init; } = SnapshotBomHandling.StripBom;

    /// <summary>How per-line trailing whitespace is treated. Defaults to
    /// <see cref="SnapshotTrailingWhitespace.Preserve"/>: whitespace differences fail the
    /// match.</summary>
    public SnapshotTrailingWhitespace TrailingWhitespace { get; init; } = SnapshotTrailingWhitespace.Preserve;

    /// <summary>How the trailing newline at end-of-file is treated. Defaults to
    /// <see cref="SnapshotTrailingNewline.Required"/>: the baseline must end with a newline
    /// or the comparison fails.</summary>
    public SnapshotTrailingNewline TrailingNewline { get; init; } = SnapshotTrailingNewline.Required;
}
