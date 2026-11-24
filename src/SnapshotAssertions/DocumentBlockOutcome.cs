namespace SnapshotAssertions;

/// <summary>The classification of a document block comparison (<see cref="DocumentBlockEvaluator"/>).</summary>
public enum DocumentBlockOutcome
{
    /// <summary>The lines between the document's markers are the actual block's lines.</summary>
    Matched,

    /// <summary>The lines between the document's markers differ from the actual block's lines. The document is left as
    /// it is.</summary>
    Mismatched,

    /// <summary>The lines differed, but accept mode was active (<c>SNAPSHOT_ACCEPT=1</c> in a non-CI environment, or
    /// with <c>SNAPSHOT_ACCEPT_IN_CI=1</c>), so the actual block was written between the markers and the comparison
    /// passes.</summary>
    Accepted,

    /// <summary>The document does not exist, or has no block: no line is the begin marker, more than one line is, or no
    /// line after it is the end marker. Accept mode does not change this outcome: a block is written only between
    /// markers that the document already has.</summary>
    BlockNotFound,
}
