namespace SnapshotAssertions;

/// <summary>How a leading byte-order mark (U+FEFF) is handled during snapshot comparison. The setting applies to
/// both compared sides, the actual content and the expected baseline. When <see cref="SnapshotEvaluator"/> reads a
/// baseline file, a byte-order mark at its start (UTF-8, UTF-16 or UTF-32) selects the encoding; under
/// <see cref="PreserveBom"/> it is returned as a leading U+FEFF, under <see cref="StripBom"/> it is dropped.</summary>
public enum SnapshotBomHandling
{
    /// <summary>Remove a leading U+FEFF from both sides before comparison. Default.</summary>
    StripBom,

    /// <summary>Keep a leading U+FEFF on both sides and compare it. <see cref="SnapshotEvaluator"/> reads a
    /// baseline file as bytes and returns its byte-order mark as a leading U+FEFF, so actual content that starts
    /// with U+FEFF matches a baseline that accept mode wrote from it.</summary>
    PreserveBom,
}
