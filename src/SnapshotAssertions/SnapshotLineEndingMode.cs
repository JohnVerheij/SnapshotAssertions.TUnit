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
}
