namespace SnapshotAssertions;

/// <summary>The pair of paths a snapshot comparison reads from and writes to.</summary>
/// <param name="ExpectedFilePath">Absolute path to the expected baseline file.</param>
/// <param name="ActualFilePath">Absolute path to the actual file written on mismatch or
/// no-baseline.</param>
public readonly record struct SnapshotPaths(string ExpectedFilePath, string ActualFilePath);
