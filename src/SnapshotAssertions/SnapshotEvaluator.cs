using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SnapshotAssertions;

/// <summary>
/// Orchestrates a single snapshot comparison: reads the expected baseline from disk if
/// present, compares against the actual content under the supplied options, applies
/// accept-mode when active, and writes the normalized actual content to the actual file
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
        var normalizedActual = SnapshotComparer.NormalizeForWrite(actualContent, options);

        if (!File.Exists(expectedPath))
        {
            if (acceptMode)
                return await AcceptAsync(expectedPath, actualPath, normalizedActual, cancellationToken).ConfigureAwait(false);

            await WriteAtomicAsync(actualPath, normalizedActual, cancellationToken).ConfigureAwait(false);
            return SnapshotResult.NoBaseline(expectedPath, actualPath);
        }

        var expectedContent = await BaselineReader.ReadAsync(expectedPath, options.BomHandling, cancellationToken).ConfigureAwait(false);

        if (SnapshotComparer.AreEqual(actualContent, expectedContent, options))
        {
            await DeleteIfExistsAsync(actualPath, cancellationToken).ConfigureAwait(false);
            return SnapshotResult.Matched(expectedPath);
        }

        if (acceptMode)
            return await AcceptAsync(expectedPath, actualPath, normalizedActual, cancellationToken).ConfigureAwait(false);

        await WriteAtomicAsync(actualPath, normalizedActual, cancellationToken).ConfigureAwait(false);

        var diff = RenderMismatchDiff(expectedContent, normalizedActual, options);
        return SnapshotResult.Mismatched(expectedPath, actualPath, diff);
    }

    /// <summary>
    /// Renders the failure diff for a mismatch: the line-by-line view of the two normalized forms,
    /// followed by the trailing-newline policy hint when a
    /// <see cref="SnapshotTrailingNewline.Forbidden"/> violation is a reason the comparison failed.
    /// The line view alone can show every line as context in that case (both sides end with a
    /// newline and are otherwise identical), so the hint names the cause explicitly. The renderer's
    /// output is newline-terminated whenever it emitted anything, and a violation implies at least
    /// one non-empty side, so the hint always starts on its own line.
    /// </summary>
    /// <param name="expectedContent">The raw expected baseline content as read from disk.</param>
    /// <param name="normalizedActual">The already-normalized actual content.</param>
    /// <param name="options">The comparison options.</param>
    /// <returns>The rendered diff text for <see cref="SnapshotResult.Mismatched"/>.</returns>
    private static string RenderMismatchDiff(string expectedContent, string normalizedActual, SnapshotOptions options)
    {
        var normalizedExpected = SnapshotComparer.NormalizeForWrite(expectedContent, options);
        var diff = LineDiffRenderer.Render(normalizedExpected, normalizedActual);
        var policyViolation = SnapshotComparer.DescribeTrailingNewlineViolation(normalizedActual, normalizedExpected, options);
        return policyViolation is null ? diff : diff + policyViolation + Environment.NewLine;
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
        await DeleteIfExistsAsync(actualPath, cancellationToken).ConfigureAwait(false);
        return SnapshotResult.Accepted(expectedPath);
    }

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/> atomically: the content goes to a
    /// uniquely-named temporary file in the same directory, which is then moved over the target. A
    /// partial or interrupted write lands on the temp, not the target, so a reader never observes a
    /// half-written baseline (the move publishes either the old or the new content, never a torn mix).
    /// The per-write temp name also lets parallel writers to <em>different</em> snapshot files run
    /// without colliding on a shared temp. Parallel writers to the <em>same</em> target (the test processes of
    /// two target frameworks accepting one project baseline, or tests that share a default name) can meet a
    /// transient sharing failure on Windows, which the move rides out by trying again; the last writer wins.
    /// </summary>
    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
            await RetryTransientAsync(() => File.Move(tempPath, path, overwrite: true), path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // On success the temp was renamed away and this is a no-op; on any failure (a throwing or
            // canceled write, or a failed move) it removes the leftover temp so the snapshots directory
            // is never littered.
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    private static Task DeleteIfExistsAsync(string path, CancellationToken cancellationToken)
        => RetryTransientAsync(
            () =>
            {
                if (File.Exists(path))
                    File.Delete(path);
            },
            path,
            cancellationToken);

    /// <summary>
    /// Runs a file operation on <paramref name="path"/> and repeats it when it fails because another evaluation of the
    /// same snapshot is replacing, reading or deleting that file at that moment. On Windows that shows as an
    /// <see cref="IOException"/> (sharing violation) or an <see cref="UnauthorizedAccessException"/> (the file is pending
    /// deletion or being replaced), and it passes within milliseconds. Two evaluations meet on one file when the test
    /// processes of two target frameworks accept the same project baseline, or when tests share a default name. The
    /// operation is repeated a bounded number of times (about one second in all), so a file that stays inaccessible, such as a
    /// read-only baseline, still fails promptly. A failure that remains is rethrown as an exception of the same kind
    /// with the path in its message, which the operating system's own text may leave out.
    /// </summary>
    /// <param name="operation">The file operation to run.</param>
    /// <param name="path">The file the operation changes.</param>
    /// <param name="cancellationToken">Cancels the wait between attempts.</param>
    /// <returns>A task that completes when the operation succeeded.</returns>
    private static async Task RetryTransientAsync(Action operation, string path, CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (true)
        {
            var failure = TryRun(operation);
            if (failure is null)
                return;

            if (attempt >= MaxFileAttempts)
                throw ToPathNamingException(failure, path);

            attempt++;
            await Task.Delay(TimeSpan.FromMilliseconds(RetryDelayMilliseconds), TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs <paramref name="operation"/> and returns the transient file failure it threw, if any.</summary>
    /// <param name="operation">The file operation.</param>
    /// <returns>The <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>, or <see langword="null"/> on success.</returns>
    private static Exception? TryRun(Action operation)
    {
        try
        {
            operation();
            return null;
        }
        catch (IOException exception)
        {
            return exception;
        }
        catch (UnauthorizedAccessException exception)
        {
            return exception;
        }
    }

    /// <summary>Wraps a failure that outlasted the attempts in an exception of the same kind whose message names the file.</summary>
    /// <param name="failure">The last failure.</param>
    /// <param name="path">The file the operation changes.</param>
    /// <returns>The exception to throw, with <paramref name="failure"/> as its inner exception.</returns>
    private static Exception ToPathNamingException(Exception failure, string path)
    {
        var message = string.Format(
            CultureInfo.InvariantCulture,
            "Access to the snapshot file '{0}' failed after repeated attempts: {1} The file may be read-only or locked by another process.",
            path,
            failure.Message);
        return failure is UnauthorizedAccessException
            ? new UnauthorizedAccessException(message, failure)
            : new IOException(message, failure);
    }

    // Declared after the methods on purpose: the compiler numbers the async state machines (<EvaluateAsync>d__N) by member
    // position, and the public EvaluateAsync records its state machine type in an attribute that package validation
    // compares with the previous release. A member declared before it renames that type and fails the comparison.

    /// <summary>How often a file operation is tried when another evaluation holds the file at that moment.</summary>
    private const int MaxFileAttempts = 50;

    /// <summary>The wait in milliseconds between two attempts of a file operation.</summary>
    private const int RetryDelayMilliseconds = 20;
}
