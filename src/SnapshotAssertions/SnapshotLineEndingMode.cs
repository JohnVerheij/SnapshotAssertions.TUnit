namespace SnapshotAssertions;

/// <summary>How line-ending differences between actual and expected content are handled
/// during snapshot comparison.</summary>
public enum SnapshotLineEndingMode
{
    /// <summary>No line-ending normalization: line endings are compared as they are.</summary>
    Ordinal,

    /// <summary>Both sides are normalized to LF before comparison. Cross-platform-safe.</summary>
    NormalizeToLF,

    /// <summary>Both sides are normalized to CRLF before comparison. Windows-leaning.</summary>
    NormalizeToCRLF,

    /// <summary>Line breaks are removed from both sides, so the text is compared as one run: blank lines and where
    /// lines break are not compared. The written actual file and an accepted baseline have no line breaks
    /// either.</summary>
    IgnoreLineEndings,
}
