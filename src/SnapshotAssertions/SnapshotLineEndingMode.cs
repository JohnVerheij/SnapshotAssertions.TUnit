namespace SnapshotAssertions;

/// <summary>How line-ending differences between actual and expected content are handled
/// during snapshot comparison.</summary>
public enum SnapshotLineEndingMode
{
    /// <summary>No normalization. Bytes are compared as-is.</summary>
    Ordinal,

    /// <summary>Both sides are normalized to LF before comparison. Cross-platform-safe.</summary>
    NormalizeToLF,

    /// <summary>Both sides are normalized to CRLF before comparison. Windows-leaning.</summary>
    NormalizeToCRLF,

    /// <summary>Line endings are removed from both sides entirely; only the line content is
    /// compared.</summary>
    IgnoreLineEndings,
}
