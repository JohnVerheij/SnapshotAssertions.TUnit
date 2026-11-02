namespace SnapshotAssertions;

/// <summary>How a leading byte-order mark (U+FEFF) in the content is handled during snapshot comparison. The
/// baseline file is read as text, so a byte-order mark at its start selects the encoding and is never part of the
/// compared content; this setting applies to the actual content.</summary>
public enum SnapshotBomHandling
{
    /// <summary>Remove a leading U+FEFF before comparison. Default.</summary>
    StripBom,

    /// <summary>Keep a leading U+FEFF in the actual content and compare it. The baseline file never yields one,
    /// so such content does not match.</summary>
    PreserveBom,
}
