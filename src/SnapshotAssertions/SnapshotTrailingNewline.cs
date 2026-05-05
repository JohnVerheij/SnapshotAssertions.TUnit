namespace SnapshotAssertions;

/// <summary>How the trailing newline at end-of-file is treated during snapshot comparison.</summary>
public enum SnapshotTrailingNewline
{
    /// <summary>The baseline must end with a single trailing newline; mismatches fail the
    /// comparison. Default.</summary>
    Required,

    /// <summary>A trailing newline on either side is accepted but not required.</summary>
    Optional,

    /// <summary>The baseline must NOT end with a trailing newline; mismatches fail the
    /// comparison.</summary>
    Forbidden,
}
