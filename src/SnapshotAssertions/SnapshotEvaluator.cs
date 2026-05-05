using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SnapshotAssertions;

/// <summary>
/// Orchestrates a single snapshot comparison: reads the expected baseline from disk if
/// present, compares against the actual content under the supplied options, applies
/// accept-mode when active, and writes the actual content to <c>.actual.txt</c> on failure.
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
    /// actual content to the actual path (or, if accept-mode is active, over the expected
    /// path) and returns a result describing the outcome.
    /// </summary>
    /// <param name="actualContent">The string produced by the test.</param>
    /// <param name="paths">The expected and actual file paths.</param>
    /// <param name="options">The comparison options.</param>
    /// <param name="acceptModeOverride">Optional override for accept-mode detection. When
    /// <see langword="null"/> (the default), the live environment is consulted via
    /// <see cref="SnapshotAcceptMode.IsActive()"/>. Tests pass an explicit value to keep their
    /// behaviour independent of the host environment.</param>
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

        cancellationToken.ThrowIfCancellationRequested();

        var expectedPath = paths.ExpectedFilePath;
        var actualPath = paths.ActualFilePath;
        var acceptMode = acceptModeOverride ?? SnapshotAcceptMode.IsActive();

        EnsureDirectoryExists(expectedPath);

        if (!File.Exists(expectedPath))
        {
            if (acceptMode)
            {
                await File.WriteAllTextAsync(expectedPath, actualContent, cancellationToken).ConfigureAwait(false);
                DeleteIfExists(actualPath);
                return SnapshotResult.Accepted(expectedPath);
            }

            await File.WriteAllTextAsync(actualPath, actualContent, cancellationToken).ConfigureAwait(false);
            return SnapshotResult.NoBaseline(expectedPath, actualPath);
        }

        var expectedContent = await File.ReadAllTextAsync(expectedPath, cancellationToken).ConfigureAwait(false);

        if (SnapshotComparer.AreEqual(actualContent, expectedContent, options))
        {
            DeleteIfExists(actualPath);
            return SnapshotResult.Matched(expectedPath);
        }

        if (acceptMode)
        {
            await File.WriteAllTextAsync(expectedPath, actualContent, cancellationToken).ConfigureAwait(false);
            DeleteIfExists(actualPath);
            return SnapshotResult.Accepted(expectedPath);
        }

        await File.WriteAllTextAsync(actualPath, actualContent, cancellationToken).ConfigureAwait(false);

        var normalisedExpected = SnapshotComparer.Normalise(expectedContent, options);
        var normalisedActual = SnapshotComparer.Normalise(actualContent, options);
        var diff = LineDiffRenderer.Render(normalisedExpected, normalisedActual);

        return SnapshotResult.Mismatched(expectedPath, actualPath, diff);
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
