using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SnapshotAssertions;

/// <summary>
/// Orchestrates a single snapshot comparison: reads the expected baseline from disk if
/// present, compares against the actual content under the supplied options, applies
/// accept-mode when active, and writes the normalized actual content to <c>.actual.txt</c>
/// on failure. Every baseline-candidate is persisted in normalized form so an accepted
/// baseline matches the form the comparison reads back.
/// </summary>
/// <remarks>
/// The evaluator is the integration point between the pure helpers (<see cref="SnapshotComparer"/>,
/// <see cref="SnapshotAcceptMode"/>, <see cref="LineDiffRenderer"/>, <see cref="SnapshotFileResolver"/>)
/// and the filesystem. Test-framework adapters call <see cref="EvaluateAsync"/> and translate
/// the returned <see cref="SnapshotResult"/> into their framework's pass/fail vocabulary.
/// </remarks>
public static class SnapshotEvaluator
{
    /// <summary>
    /// Compares <paramref name="actualContent"/> against the baseline at
    /// <paramref name="paths"/>'s expected path. On mismatch or missing baseline, writes the
    /// normalized actual content to the actual path (or, if accept-mode is active, over the
    /// expected path) and returns a result describing the outcome. The written form is the
    /// option-driven normalized form, not the raw subject.
    /// </summary>
    /// <param name="actualContent">The string produced by the test.</param>
    /// <param name="paths">The expected and actual file paths.</param>
    /// <param name="options">The comparison options.</param>
    /// <param name="acceptModeOverride">Optional override for accept-mode detection. When
    /// <see langword="null"/> (the default), the live environment is consulted via
    /// <see cref="SnapshotAcceptMode.IsActive()"/>. Tests pass an explicit value to keep their
    /// behavior independent of the host environment.</param>
    /// <param name="cancellationToken">Cancellation token propagated to the underlying file IO.</param>
    /// <returns>The comparison outcome.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static async Task<SnapshotResult> EvaluateAsync(
        string actualContent,
        SnapshotPaths paths,
        SnapshotOptions options,
        bool? acceptModeOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actualContent);
        ArgumentNullException.ThrowIfNull(options);

        // SnapshotPaths is a struct so cannot itself be null, but its string fields can if a
        // caller constructed it directly with null. Validate them so the contract documented
        // on the method (ArgumentNullException for null inputs) holds end-to-end rather than
        // surfacing as a NullReferenceException from File.WriteAllTextAsync downstream.
        if (paths.ExpectedFilePath is null)
            throw new ArgumentNullException(nameof(paths), "paths.ExpectedFilePath must not be null.");
        if (paths.ActualFilePath is null)
            throw new ArgumentNullException(nameof(paths), "paths.ActualFilePath must not be null.");

        cancellationToken.ThrowIfCancellationRequested();

        var expectedPath = paths.ExpectedFilePath;
        var actualPath = paths.ActualFilePath;
        var acceptMode = acceptModeOverride ?? SnapshotAcceptMode.IsActive();

        // Ensure both directories exist. The expected and actual paths usually share a parent
        // directory (default file resolver puts both under Snapshots/), but ResolveByFile lets
        // a caller supply an explicit expected path whose actual sibling may live elsewhere.
        EnsureDirectoryExists(expectedPath);
        EnsureDirectoryExists(actualPath);

        // Every baseline-candidate written to disk (the .actual file a consumer renames to
        // accept, and the expected file accept-mode overwrites) is written in normalized form,
        // never raw. The comparison applies the same normalization to both sides, so a raw
        // write would let an accepted baseline carry un-canonicalized / unscrubbed volatile
        // text that the very next comparison normalizes away. Writing the normalized form keeps
        // the committed baseline consistent with what the read path compares against, matching
        // how Verify scrubs before writing its .received file.
        var normalizedActual = SnapshotComparer.Normalize(actualContent, options);

        if (!File.Exists(expectedPath))
        {
            if (acceptMode)
                return await AcceptAsync(expectedPath, actualPath, normalizedActual, cancellationToken).ConfigureAwait(false);

            await WriteAtomicAsync(actualPath, normalizedActual, cancellationToken).ConfigureAwait(false);
            return SnapshotResult.NoBaseline(expectedPath, actualPath);
        }

        var expectedContent = await File.ReadAllTextAsync(expectedPath, cancellationToken).ConfigureAwait(false);

        if (SnapshotComparer.AreEqual(actualContent, expectedContent, options))
        {
            DeleteIfExists(actualPath);
            return SnapshotResult.Matched(expectedPath);
        }

        if (acceptMode)
            return await AcceptAsync(expectedPath, actualPath, normalizedActual, cancellationToken).ConfigureAwait(false);

        await WriteAtomicAsync(actualPath, normalizedActual, cancellationToken).ConfigureAwait(false);

        var normalizedExpected = SnapshotComparer.Normalize(expectedContent, options);
        var diff = LineDiffRenderer.Render(normalizedExpected, normalizedActual);

        return SnapshotResult.Mismatched(expectedPath, actualPath, diff);
    }

    /// <summary>
    /// Writes <paramref name="normalizedContent"/> over the expected baseline (accept-mode),
    /// removes any stale actual sibling, and returns an <see cref="SnapshotResult.Accepted"/>
    /// result. The content is already normalized by the caller so the persisted baseline
    /// matches the form the comparison reads back.
    /// </summary>
    /// <param name="expectedPath">The expected baseline path to overwrite.</param>
    /// <param name="actualPath">The actual sibling path to delete if present.</param>
    /// <param name="normalizedContent">The normalized content to persist.</param>
    /// <param name="cancellationToken">Cancellation token propagated to the file IO.</param>
    /// <returns>An accepted result for <paramref name="expectedPath"/>.</returns>
    private static async Task<SnapshotResult> AcceptAsync(
        string expectedPath,
        string actualPath,
        string normalizedContent,
        CancellationToken cancellationToken)
    {
        await WriteAtomicAsync(expectedPath, normalizedContent, cancellationToken).ConfigureAwait(false);
        DeleteIfExists(actualPath);
        return SnapshotResult.Accepted(expectedPath);
    }

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/> atomically: the content goes to a
    /// uniquely-named temporary file in the same directory, which is then moved over the target. A
    /// partial or interrupted write cannot leave a half-written baseline, and two writers racing the
    /// same target each move their own temp, so the loser is overwritten cleanly instead of throwing a
    /// sharing violation mid-write (common on Windows under parallel test execution).
    /// </summary>
    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var tempPath = Path.Combine(directory, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
        try
        {
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            // On success the temp was renamed away and this is a no-op; on failure it removes the
            // leftover temp so a failed write does not litter the snapshots directory.
            DeleteIfExists(tempPath);
        }
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
