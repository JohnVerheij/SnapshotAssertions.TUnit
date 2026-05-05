namespace SnapshotAssertions;

/// <summary>How a leading byte-order-mark is handled during snapshot comparison.</summary>
public enum SnapshotBomHandling
{
    /// <summary>Strip a UTF-8 BOM from both actual and expected content before comparison.
    /// Default.</summary>
    StripBom,

    /// <summary>Preserve any BOM bytes; treat them as part of the content for comparison
    /// purposes.</summary>
    PreserveBom,
}
