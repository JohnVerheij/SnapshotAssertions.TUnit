using SnapshotAssertions;
using SnapshotAssertions.TUnit;
using TUnit.Assertions.Core;

namespace TUnit.Assertions.Extensions;

/// <summary>
/// Top-level shorthand entry points that wrap the most common <c>MatchesSnapshot()...</c>
/// chains. Each shorthand is equivalent to spelling out the underlying chain — they exist
/// purely to reduce ceremony.
/// </summary>
/// <remarks>
/// Lives in <c>TUnit.Assertions.Extensions</c> (where TUnit's source generator emits the core
/// entry-point extension method <c>MatchesSnapshot()</c>) so consumers do not need a second
/// <c>using</c> directive to discover these shorthands. If you can call
/// <c>Assert.That(actual).MatchesSnapshot()</c> in a file, you can also call
/// <c>Assert.That(actual).MatchesSnapshot("custom-name")</c> and
/// <c>Assert.That(actual).MatchesSnapshotFile("/path/to/file.txt")</c> there — same auto-import.
/// </remarks>
public static class MatchesSnapshotShorthandExtensions
{
    /// <summary>
    /// Asserts that <paramref name="source"/>'s actual content matches the snapshot baseline
    /// identified by <paramref name="snapshotName"/> under the project's <c>Snapshots/</c>
    /// directory. Equivalent to <c>source.MatchesSnapshot().WithName(snapshotName)</c>.
    /// </summary>
    /// <param name="source">The assertion source over a <see cref="string"/>.</param>
    /// <param name="snapshotName">The base name (without extension).</param>
    /// <returns>The assertion configured for the explicit name.</returns>
    public static SnapshotAssertion MatchesSnapshot(this IAssertionSource<string> source, string snapshotName)
        => source.MatchesSnapshot().WithName(snapshotName);

    /// <summary>
    /// Asserts that <paramref name="source"/>'s actual content matches the snapshot baseline
    /// (resolved via the active TUnit test context), comparing under the supplied
    /// <paramref name="options"/>. Equivalent to <c>source.MatchesSnapshot().WithOptions(options)</c>.
    /// </summary>
    /// <param name="source">The assertion source over a <see cref="string"/>.</param>
    /// <param name="options">The comparison options.</param>
    /// <returns>The assertion configured for the supplied options.</returns>
    public static SnapshotAssertion MatchesSnapshot(this IAssertionSource<string> source, SnapshotOptions options)
        => source.MatchesSnapshot().WithOptions(options);

    /// <summary>
    /// Asserts that <paramref name="source"/>'s actual content matches the snapshot baseline
    /// identified by <paramref name="snapshotName"/>, comparing under the supplied
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="source">The assertion source over a <see cref="string"/>.</param>
    /// <param name="snapshotName">The base name (without extension).</param>
    /// <param name="options">The comparison options.</param>
    /// <returns>The assertion configured for the explicit name and options.</returns>
    public static SnapshotAssertion MatchesSnapshot(this IAssertionSource<string> source, string snapshotName, SnapshotOptions options)
        => source.MatchesSnapshot().WithName(snapshotName).WithOptions(options);

    /// <summary>
    /// Asserts that <paramref name="source"/>'s actual content matches the snapshot baseline
    /// at the explicit absolute or relative <paramref name="filePath"/>. Equivalent to
    /// <c>source.MatchesSnapshot().AtPath(filePath)</c>.
    /// </summary>
    /// <param name="source">The assertion source over a <see cref="string"/>.</param>
    /// <param name="filePath">The path to the expected baseline file.</param>
    /// <returns>The assertion configured for the explicit path.</returns>
    public static SnapshotAssertion MatchesSnapshotFile(this IAssertionSource<string> source, string filePath)
        => source.MatchesSnapshot().AtPath(filePath);

    /// <summary>
    /// Asserts that <paramref name="source"/>'s actual content matches the snapshot baseline
    /// at the explicit <paramref name="filePath"/>, comparing under the supplied
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="source">The assertion source over a <see cref="string"/>.</param>
    /// <param name="filePath">The path to the expected baseline file.</param>
    /// <param name="options">The comparison options.</param>
    /// <returns>The assertion configured for the explicit path and options.</returns>
    public static SnapshotAssertion MatchesSnapshotFile(this IAssertionSource<string> source, string filePath, SnapshotOptions options)
        => source.MatchesSnapshot().AtPath(filePath).WithOptions(options);
}
