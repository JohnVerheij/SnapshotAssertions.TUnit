using System;
using System.Collections.Generic;
using System.IO;
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
    /// The runtime host configuration option the package's build targets set to the consuming project's
    /// directory. Internal on purpose: no public member exposes the key.
    /// </summary>
    internal const string ProjectDirectoryKey = "SnapshotAssertions.ProjectDirectory";

    /// <summary>
    /// Applies the supplied scrubber chain (if any), evaluates the snapshot against the
    /// resolved paths, and projects the result to a TUnit <see cref="AssertionResult"/>.
    /// IO failures from the evaluator propagate to the caller rather than being wrapped as
    /// a failed assertion; the misuse signal is more useful raw at the call site. When
    /// <paramref name="projectBaselinePath"/> is supplied, a failure message gets one extra line after the
    /// core description naming that project-side path.
    /// </summary>
    /// <param name="content">The actual text.</param>
    /// <param name="scrubbers">The scrubber chain to apply first, or <see langword="null"/>.</param>
    /// <param name="options">The comparison options.</param>
    /// <param name="paths">The resolved expected and actual file paths.</param>
    /// <param name="projectBaselinePath">The baseline path under the project's <c>Snapshots/</c> folder, or
    /// <see langword="null"/> when the project folder is unknown or the baseline path was set explicitly.</param>
    /// <param name="cancellationToken">A token to cancel the evaluation.</param>
    /// <returns>The TUnit assertion result.</returns>
    public static async Task<AssertionResult> EvaluateAsync(
        string content,
        IReadOnlyList<SnapshotScrubber>? scrubbers,
        SnapshotOptions options,
        SnapshotPaths paths,
        string? projectBaselinePath = null,
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
            : AssertionResult.Failed(DescribeFailure(result, projectBaselinePath));
    }

    /// <summary>
    /// Builds the failure message: the core description, plus a <c>Project baseline:</c> line when the
    /// project-side path is known.
    /// </summary>
    /// <param name="result">The failed snapshot result.</param>
    /// <param name="projectBaselinePath">The project-side baseline path, or <see langword="null"/>.</param>
    /// <returns>The message text.</returns>
    private static string DescribeFailure(SnapshotResult result, string? projectBaselinePath)
    {
        var description = result.Describe();
        return projectBaselinePath is null
            ? description
            : description + "Project baseline: " + projectBaselinePath + Environment.NewLine;
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
    /// <returns>The expected and actual paths.</returns>
    /// <exception cref="InvalidOperationException">No explicit name or path was supplied and
    /// no active TUnit test context is available.</exception>
    public static SnapshotPaths ResolvePaths(string? explicitPath, string? explicitName, bool? acceptModeOverride = null)
        => Resolve(explicitPath, explicitName, acceptModeOverride, AppContext.BaseDirectory, GetProjectDirectory()).Paths;

    /// <summary>
    /// Resolves the snapshot paths and, for a default-named or <c>.WithName(...)</c> baseline whose project
    /// folder is known, the baseline path under the project's <c>Snapshots/</c> folder.
    /// </summary>
    /// <param name="explicitPath">An explicit baseline file path, or <see langword="null"/>.</param>
    /// <param name="explicitName">An explicit snapshot name, or <see langword="null"/>.</param>
    /// <param name="acceptModeOverride">Optional accept-mode override; <see langword="null"/> consults the
    /// live environment.</param>
    /// <param name="baseDirectory">The runtime base directory, normally <see cref="AppContext.BaseDirectory"/>.</param>
    /// <param name="projectDirectory">The project directory recorded by the build, normally from
    /// <see cref="GetProjectDirectory"/>; <see langword="null"/> or a missing directory means unknown.</param>
    /// <returns>The paths, plus the project-side baseline path (<see langword="null"/> for an explicit path or an
    /// unknown project folder).</returns>
    /// <exception cref="InvalidOperationException">No explicit name or path was supplied and
    /// no active TUnit test context is available.</exception>
    internal static ResolvedSnapshot Resolve(
        string? explicitPath,
        string? explicitName,
        bool? acceptModeOverride,
        string baseDirectory,
        string? projectDirectory)
    {
        if (explicitPath is not null)
            return new ResolvedSnapshot(SnapshotFileResolver.ResolveByFile(explicitPath), ProjectBaselinePath: null);

        var directory = ResolveSnapshotsDirectory(acceptModeOverride, baseDirectory, projectDirectory);
        var paths = ResolveNamed(directory, explicitName);

        var knownProject = ExistingDirectoryOrNull(projectDirectory);
        var projectBaseline = knownProject is null
            ? null
            : ResolveNamed(ProjectSnapshotsDirectory(knownProject), explicitName).ExpectedFilePath;
        return new ResolvedSnapshot(paths, projectBaseline);
    }

    /// <summary>Resolves a name- or test-context-derived snapshot inside <paramref name="directory"/>.</summary>
    /// <param name="directory">The snapshots directory.</param>
    /// <param name="explicitName">An explicit snapshot name, or <see langword="null"/> to derive it from the
    /// active TUnit test.</param>
    /// <returns>The expected and actual paths.</returns>
    /// <exception cref="InvalidOperationException">No name was supplied and no TUnit test context is active.</exception>
    private static SnapshotPaths ResolveNamed(string directory, string? explicitName)
    {
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
        return SnapshotFileResolver.ResolveByTest(
            directory, className, details.TestClassArguments, methodName, details.TestMethodArguments);
    }

    /// <summary>
    /// Reads the project directory the package's build targets record as the runtime host configuration
    /// option <see cref="ProjectDirectoryKey"/> (no reflection).
    /// </summary>
    /// <returns>The directory when the option is set to an existing directory, otherwise <see langword="null"/>.</returns>
    internal static string? GetProjectDirectory()
        => ExistingDirectoryOrNull(AppContext.GetData(ProjectDirectoryKey) as string);

    /// <summary>Returns <paramref name="path"/> when it names an existing directory, otherwise <see langword="null"/>.</summary>
    /// <param name="path">A candidate directory path, or <see langword="null"/>.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    private static string? ExistingDirectoryOrNull(string? path)
        => !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : null;

    /// <summary>Returns the <c>Snapshots/</c> folder under a project directory.</summary>
    /// <param name="projectDirectory">The project directory.</param>
    /// <returns>The absolute snapshots folder path.</returns>
    private static string ProjectSnapshotsDirectory(string projectDirectory)
        => Path.GetFullPath(Path.Combine(projectDirectory, SnapshotFileResolver.DefaultSnapshotsFolder));

    /// <summary>
    /// Picks the snapshots directory used for name- and test-context-derived resolution. The
    /// read path uses the runtime copy under <c>bin/</c> (<see cref="AppContext.BaseDirectory"/>);
    /// accept-mode instead targets the source-tree <c>Snapshots/</c> folder so the accepted
    /// baseline lands where it is committed. That folder is the recorded project directory when known
    /// (see <see cref="GetProjectDirectory"/>), otherwise the nearest ancestor of the base directory with a
    /// <c>*.csproj</c>, <c>*.fsproj</c> or <c>*.vbproj</c>. Falls back to the runtime <c>Snapshots/</c> folder
    /// when neither can be found (e.g. a single-file publish).
    /// </summary>
    /// <param name="acceptModeOverride">Optional accept-mode override; <see langword="null"/>
    /// consults the live environment.</param>
    /// <param name="baseDirectory">The runtime base directory to resolve from, normally
    /// <see cref="AppContext.BaseDirectory"/>. Injectable so the source-tree fallback can be
    /// exercised from a directory with no ancestor project file.</param>
    /// <param name="projectDirectory">The project directory recorded by the build, or <see langword="null"/>
    /// when unknown. Injectable like <paramref name="baseDirectory"/>.</param>
    /// <returns>The absolute snapshots directory to resolve against.</returns>
    internal static string ResolveSnapshotsDirectory(bool? acceptModeOverride, string baseDirectory, string? projectDirectory)
    {
        var runtimeDirectory = SnapshotFileResolver.GetDefaultSnapshotsDirectory(baseDirectory);

        var acceptMode = acceptModeOverride ?? SnapshotAcceptMode.IsActive();
        if (!acceptMode)
            return runtimeDirectory;

        var knownProject = ExistingDirectoryOrNull(projectDirectory);
        if (knownProject is not null)
            return ProjectSnapshotsDirectory(knownProject);

        return SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(baseDirectory)
            ?? runtimeDirectory;
    }
}
