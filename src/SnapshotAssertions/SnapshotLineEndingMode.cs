namespace SnapshotAssertions;

/// <summary>How line-ending differences between actual and expected content are handled
/// during snapshot comparison.</summary>
public enum SnapshotLineEndingMode
{
    /// <summary>No line-ending normalization: each line's ending is compared as it is, so CRLF and LF differ. Under
    /// <see cref="SnapshotTrailingNewline.Optional"/> the final line's ending, whichever it is, is not
    /// compared.</summary>
    Ordinal,

    /// <summary>Both sides are normalized to LF before comparison. Cross-platform-safe.</summary>
    NormalizeToLF,

    /// <summary>Both sides are normalized to CRLF before comparison. Windows-leaning.</summary>
    NormalizeToCRLF,

    /// <summary>Line breaks are removed from both sides, so the text is compared as one run: blank lines and where
    /// lines break are not compared. A final newline is kept as one LF unless
    /// <see cref="SnapshotOptions.TrailingNewline"/> is <see cref="SnapshotTrailingNewline.Optional"/> (under the
    /// default <see cref="SnapshotTrailingNewline.Required"/>, <c>"a\nb\n"</c> becomes <c>"ab\n"</c>), so the written
    /// actual file and an accepted baseline have no line break other than that final one.</summary>
    IgnoreLineEndings,
}
