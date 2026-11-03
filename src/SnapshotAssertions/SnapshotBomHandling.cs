namespace SnapshotAssertions;

/// <summary>How a leading byte-order mark (U+FEFF) is handled during snapshot comparison. The setting applies to
/// both compared sides, the actual content and the expected baseline. When <see cref="SnapshotEvaluator"/> reads a
/// baseline file, a byte-order mark at its start selects the encoding and is consumed by the read.</summary>
public enum SnapshotBomHandling
{
    /// <summary>Remove a leading U+FEFF from both sides before comparison. Default.</summary>
    StripBom,

    /// <summary>Keep a leading U+FEFF on both sides and compare it. <see cref="SnapshotEvaluator"/> consumes a
    /// baseline file's byte-order mark when it reads the file, so actual content that starts with U+FEFF does not
    /// match a baseline that accept mode wrote from it.</summary>
    PreserveBom,
}
