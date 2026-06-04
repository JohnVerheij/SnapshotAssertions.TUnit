using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using TUnit.Assertions.Core;
using TUnit.Core;

namespace SnapshotAssertions.TUnit;

/// <summary>
/// Shared pipeline used by both <see cref="SnapshotAssertion"/> (string source) and
/// <see cref="RenderedSnapshotAssertion{T}"/> (typed source + renderer). Pulls the
/// path-resolution and scrubber-apply-then-evaluate logic out so the renderer-based
/// assertion does not have to duplicate it.
/// </summary>
internal static class SnapshotAssertionImpl
{
    /// <summary>
    /// Applies the supplied scrubber chain (if any), evaluates the snapshot against the
    /// resolved paths, and projects the result to a TUnit <see cref="AssertionResult"/>.
    /// IO failures from the evaluator propagate to the caller rather than being wrapped as
    /// a failed assertion; the misuse signal is more useful raw at the call site.
    /// </summary>
    public static async Task<AssertionResult> EvaluateAsync(
        string content,
        IReadOnlyList<SnapshotScrubber>? scrubbers,
        SnapshotOptions options,
        SnapshotPaths paths,
        CancellationToken cancellationToken = default)
    {
        if (scrubbers is { Count: > 0 })
        {
            var state = new SnapshotScrubberState();
            for (var i = 0; i < scrubbers.Count; i++)
            {
                content = scrubbers[i].Apply(content, state);
            }
        }

        var result = await SnapshotEvaluator.EvaluateAsync(content, paths, options, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.IsPass
            ? AssertionResult.Passed
            : AssertionResult.Failed(result.Describe());
    }

    /// <summary>
    /// Resolves the expected / actual file paths for a snapshot. Honours an explicit file
    /// path or explicit name; otherwise derives the name from the active TUnit test context.
    /// </summary>
    /// <remarks>
    /// When accept-mode is active (<see cref="SnapshotAcceptMode.IsActive()"/>), name- and
    /// test-context-derived snapshots resolve against the source-tree <c>Snapshots/</c> folder
    /// rather than the runtime copy under <c>bin/</c>, so that the accepted baseline is written
    /// where it is committed and read from. An explicit <c>.AtPath(...)</c> already points at a
    /// caller-chosen file and is never redirected.
    /// </remarks>
    /// <param name="explicitPath">An explicit baseline file path (from <c>.AtPath(...)</c>), or
    /// <see langword="null"/>.</param>
    /// <param name="explicitName">An explicit snapshot name (from <c>.WithName(...)</c>), or
    /// <see langword="null"/>.</param>
    /// <param name="acceptModeOverride">Optional override for accept-mode detection. When
    /// <see langword="null"/> (the default), the live environment is consulted. Tests pass an
    /// explicit value to keep their behavior independent of the host environment.</param>
    /// <exception cref="InvalidOperationException">No explicit name or path was supplied and
    /// no active TUnit test context is available.</exception>
    public static SnapshotPaths ResolvePaths(string? explicitPath, string? explicitName, bool? acceptModeOverride = null)
    {
        if (explicitPath is not null)
            return SnapshotFileResolver.ResolveByFile(explicitPath);

        var directory = ResolveSnapshotsDirectory(acceptModeOverride, AppContext.BaseDirectory);

        if (explicitName is not null)
            return SnapshotFileResolver.ResolveByName(directory, explicitName);

        var testContext = TestContext.Current
            ?? throw new InvalidOperationException(
                "MatchesSnapshot() with no name or explicit path requires an active TUnit test context " +
                "(TestContext.Current was null). Call from inside a [Test] method, pass a snapshot name " +
                "via .WithName(\"...\") (or the MatchesSnapshot(name) shorthand), or pass an explicit file " +
                "path via .AtPath(\"...\") (or MatchesSnapshotFile(path)).");

        var details = testContext.Metadata.TestDetails;
        var className = details.ClassType.Name;
        var methodName = details.MethodName;
        var args = details.TestMethodArguments;
        return SnapshotFileResolver.ResolveByTest(directory, className, methodName, args);
    }

    /// <summary>
    /// Picks the snapshots directory used for name- and test-context-derived resolution. The
    /// read path uses the runtime copy under <c>bin/</c> (<see cref="AppContext.BaseDirectory"/>);
    /// accept-mode instead targets the source-tree <c>Snapshots/</c> folder so the accepted
    /// baseline lands where it is committed. Falls back to the runtime directory when the
    /// source tree cannot be located (e.g. a single-file publish).
    /// </summary>
    /// <param name="acceptModeOverride">Optional accept-mode override; <see langword="null"/>
    /// consults the live environment.</param>
    /// <param name="baseDirectory">The runtime base directory to resolve from, normally
    /// <see cref="AppContext.BaseDirectory"/>. Injectable so the source-tree fallback can be
    /// exercised from a directory with no ancestor project file.</param>
    /// <returns>The absolute snapshots directory to resolve against.</returns>
    internal static string ResolveSnapshotsDirectory(bool? acceptModeOverride, string baseDirectory)
    {
        var runtimeDirectory = SnapshotFileResolver.GetDefaultSnapshotsDirectory(baseDirectory);

        var acceptMode = acceptModeOverride ?? SnapshotAcceptMode.IsActive();
        if (!acceptMode)
            return runtimeDirectory;

        return SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(baseDirectory)
            ?? runtimeDirectory;
    }
}
