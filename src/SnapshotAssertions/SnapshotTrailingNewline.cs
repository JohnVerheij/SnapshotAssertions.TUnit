namespace SnapshotAssertions;

/// <summary>How the trailing newline at end-of-file is treated during snapshot comparison.</summary>
public enum SnapshotTrailingNewline
{
    /// <summary>The trailing-newline state of both sides is preserved and compared: a baseline
    /// that ends with a newline only matches actual content that also ends with one, and a
    /// baseline without one only matches actual content without one. Default.</summary>
    Required,

    /// <summary>A trailing newline on either side is accepted but not required.</summary>
    Optional,

    /// <summary>Neither side may end with a trailing newline. A trailing newline on the expected
    /// baseline or on the actual content fails the comparison, even when both sides are otherwise
    /// identical. The failure output names the offending side.</summary>
    Forbidden,
}
