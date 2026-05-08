using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;
using TUnit.Core;

namespace SnapshotAssertions.TUnit;

/// <summary>
/// TUnit assertion that verifies an actual <see cref="string"/> matches a baseline snapshot
/// stored on disk. The default file resolution uses the current TUnit test context's class
/// and method names; chain methods (<see cref="WithName"/>, <see cref="AtPath"/>,
/// <see cref="WithOptions"/>) override the defaults.
/// </summary>
/// <remarks>
/// <para>
/// On mismatch or missing baseline, the actual content is written to a sibling
/// <c>.actual.txt</c> file and the assertion fails with both paths and a line-based diff in
/// the failure message. When the <c>SNAPSHOT_ACCEPT</c> environment variable is set to a
/// truthy value (and the <c>CI</c> environment variable is not set), the actual content is
/// instead written over the expected baseline and the assertion passes; this is the
/// accept-mode used to bulk-update snapshots after intentional changes.
/// </para>
/// </remarks>
[AssertionExtension("MatchesSnapshot")]
public sealed class SnapshotAssertion : Assertion<string>
{
    private string? _explicitName;
    private string? _explicitPath;
    private SnapshotOptions _options = SnapshotOptions.Default;
    private List<SnapshotScrubber>? _scrubbers;

    /// <summary>Initialises the assertion. Called by the TUnit source generator.</summary>
    /// <param name="context">The assertion context supplied by TUnit.</param>
    public SnapshotAssertion(AssertionContext<string> context) : base(context) { }

    /// <summary>
    /// Overrides the default TUnit-test-derived snapshot name. Useful when multiple snapshots
    /// are produced by a single test method (e.g. before/after states).
    /// </summary>
    /// <param name="snapshotName">The base name (without extension) under the project's
    /// <c>Snapshots/</c> directory.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshotName"/> is <see langword="null"/>.</exception>
    public SnapshotAssertion WithName(string snapshotName)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);
        _explicitName = snapshotName;
        Context.ExpressionBuilder.Append(CultureInfo.InvariantCulture, $".WithName(\"{snapshotName}\")");
        return this;
    }

    /// <summary>
    /// Overrides path resolution entirely with an explicit absolute or relative file path to
    /// the expected baseline. The actual file is sibling to the expected file.
    /// </summary>
    /// <param name="filePath">The path to the expected baseline file.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    public SnapshotAssertion AtPath(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        _explicitPath = filePath;
        Context.ExpressionBuilder.Append(CultureInfo.InvariantCulture, $".AtPath(\"{filePath}\")");
        return this;
    }

    /// <summary>Overrides the comparison options (line-ending handling, BOM, trailing
    /// whitespace, trailing newline).</summary>
    /// <param name="options">The options to apply.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public SnapshotAssertion WithOptions(SnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        Context.ExpressionBuilder.Append(".WithOptions(...)");
        return this;
    }

    /// <summary>
    /// Adds a <see cref="SnapshotScrubber"/> to the pipeline. Multiple <c>.WithScrubber()</c>
    /// calls compose left-to-right: the first scrubber receives the raw actual content; each
    /// subsequent scrubber receives the previous scrubber's output. All scrubbers in the chain
    /// share a single <see cref="SnapshotScrubberState"/> so recurring volatile values keep a
    /// stable indexed token across the snapshot.
    /// </summary>
    /// <param name="scrubber">The scrubber to append. Use <see cref="Scrubbers.Default"/> for
    /// the curated GUID + ISO 8601 + Unix-millis chain, or one of the individual
    /// <see cref="Scrubbers"/> properties / <see cref="Scrubbers.Pattern(string, string)"/>
    /// factories.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scrubber"/> is <see langword="null"/>.</exception>
    public SnapshotAssertion WithScrubber(SnapshotScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(scrubber);
        _scrubbers ??= [];
        _scrubbers.Add(scrubber);
        Context.ExpressionBuilder.Append(".WithScrubber(...)");
        return this;
    }

    /// <inheritdoc/>
    protected override async Task<AssertionResult> CheckAsync(EvaluationMetadata<string> metadata)
    {
        if (metadata.Exception is not null)
        {
            return AssertionResult.Failed(
                $"threw {metadata.Exception.GetType().Name}", metadata.Exception);
        }

        var content = metadata.Value;
        if (content is null)
            return AssertionResult.Failed("actual content was null");

        // Apply scrubbers (if any) before path resolution / evaluation. The scrubber state is
        // local to this single MatchesSnapshot() evaluation; recurring volatile values get the
        // same indexed token across the whole snapshot but state never crosses test boundaries.
        if (_scrubbers is { Count: > 0 })
        {
            var state = new SnapshotScrubberState();
            foreach (var scrubber in _scrubbers)
            {
                content = scrubber.Apply(content, state);
            }
        }

        // ResolvePaths intentionally lets InvalidOperationException (no test context) propagate
        // to the test runner rather than downgrading it to AssertionResult.Failed. The misuse
        // signal: "you called MatchesSnapshot() with no name or explicit path outside an
        // active TUnit test method": is more useful as a raw exception that surfaces at the
        // call site than as a generic failed assertion message. Likewise, IO failures
        // (filesystem permissions, disk full, etc.) propagate from EvaluateAsync.
        var paths = ResolvePaths();
        var result = await SnapshotEvaluator.EvaluateAsync(content, paths, _options).ConfigureAwait(false);
        return result.IsPass
            ? AssertionResult.Passed
            : AssertionResult.Failed(result.Describe());
    }

    /// <inheritdoc/>
    protected override string GetExpectation() => "to match the snapshot baseline";

    private SnapshotPaths ResolvePaths()
    {
        if (_explicitPath is not null)
            return SnapshotFileResolver.ResolveByFile(_explicitPath);

        var directory = SnapshotFileResolver.GetDefaultSnapshotsDirectory(AppContext.BaseDirectory);

        if (_explicitName is not null)
            return SnapshotFileResolver.ResolveByName(directory, _explicitName);

        var testContext = TestContext.Current
            ?? throw new InvalidOperationException(
                "MatchesSnapshot() with no name or explicit path requires an active TUnit test context " +
                "(TestContext.Current was null). Call from inside a [Test] method, pass a snapshot name " +
                "via .WithName(\"...\") (or the MatchesSnapshot(name) shorthand), or pass an explicit file " +
                "path via .AtPath(\"...\") (or MatchesSnapshotFile(path)).");

        var details = testContext.Metadata.TestDetails;
        var className = details.ClassType.Name;
        var methodName = details.MethodName;
        // Pass through the test-method arguments so parameterized tests get distinct
        // snapshot files per argument set. SnapshotFileResolver.ResolveByTest computes a
        // stable hash and appends it to the file name when arguments are present.
        var args = details.TestMethodArguments;
        return SnapshotFileResolver.ResolveByTest(directory, className, methodName, args);
    }
}
