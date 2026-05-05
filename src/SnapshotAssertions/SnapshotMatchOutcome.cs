namespace SnapshotAssertions;

/// <summary>The classification of a snapshot comparison outcome.</summary>
public enum SnapshotMatchOutcome
{
    /// <summary>Actual content matched the expected baseline byte-for-byte (after applicable
    /// option-driven normalisation).</summary>
    Matched,

    /// <summary>Actual content did not match the expected baseline. Diff and paths are available
    /// on the result.</summary>
    Mismatched,

    /// <summary>The expected baseline file does not exist. The first run after introducing a
    /// snapshot test produces this outcome; the actual content is written next to where the
    /// expected file would be so the user can inspect and accept it.</summary>
    NoBaseline,

    /// <summary>The actual content did not match, but accept-mode was active
    /// (<c>SNAPSHOT_ACCEPT=1</c> in a non-CI environment), so the actual content was written
    /// over the expected baseline and the comparison treats this as a pass.</summary>
    Accepted,
}
