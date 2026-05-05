namespace SnapshotAssertions;

/// <summary>How per-line trailing whitespace is treated during snapshot comparison.</summary>
public enum SnapshotTrailingWhitespace
{
    /// <summary>Preserve trailing whitespace on every line; whitespace differences fail the
    /// match. Default.</summary>
    Preserve,

    /// <summary>Trim trailing whitespace from each line on both sides before comparison.</summary>
    TrimTrailingPerLine,
}
