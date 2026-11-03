using SnapshotAssertions;

namespace SnapshotAssertions.TUnit;

/// <summary>The result of <see cref="SnapshotAssertionImpl.Resolve"/>.</summary>
/// <param name="Paths">The expected and actual file paths the evaluation uses.</param>
/// <param name="ProjectBaselinePath">The baseline path under the project's <c>Snapshots/</c> folder, or
/// <see langword="null"/> for an explicit path or an unknown project folder.</param>
internal readonly record struct ResolvedSnapshot(SnapshotPaths Paths, string? ProjectBaselinePath);
