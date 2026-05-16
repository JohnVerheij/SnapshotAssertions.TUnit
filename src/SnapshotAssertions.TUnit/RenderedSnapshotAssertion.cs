using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.Render;
using TUnit.Assertions.Core;

namespace SnapshotAssertions.TUnit;

/// <summary>
/// Renderer-projected TUnit assertion that verifies a value of type <typeparamref name="T"/>,
/// after canonical-string rendering via a <see cref="SnapshotRenderer{T}"/>, matches a
/// baseline snapshot stored on disk. Used by the
/// <c>MatchesSnapshot&lt;T&gt;(IAssertionSource&lt;T&gt;, SnapshotRenderer&lt;T&gt;)</c> and
/// <c>MatchesSnapshot&lt;T&gt;(IAssertionSource&lt;T&gt;, Func&lt;T, string&gt;)</c>
/// overloads. Shares the underlying scrubber / path / evaluator pipeline with
/// <see cref="SnapshotAssertion"/> via <see cref="SnapshotAssertionImpl"/>.
/// </summary>
/// <typeparam name="T">The source type that the renderer projects to a canonical string.</typeparam>
public sealed class RenderedSnapshotAssertion<T> : Assertion<T>
{
    private readonly SnapshotRenderer<T> _renderer;
    private string? _explicitName;
    private string? _explicitPath;
    private SnapshotOptions _options = SnapshotOptions.Default;
    private List<SnapshotScrubber>? _scrubbers;

    /// <summary>Initialises the renderer-projected assertion.</summary>
    /// <param name="context">The assertion context supplied by TUnit.</param>
    /// <param name="renderer">The renderer projecting <typeparamref name="T"/> to a
    /// canonical string. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is <see langword="null"/>.</exception>
    public RenderedSnapshotAssertion(AssertionContext<T> context, SnapshotRenderer<T> renderer)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    /// <summary>Overrides the default TUnit-test-derived snapshot name.</summary>
    /// <param name="snapshotName">The base name (without extension).</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshotName"/> is <see langword="null"/>.</exception>
    public RenderedSnapshotAssertion<T> WithName(string snapshotName)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);
        _explicitName = snapshotName;
        Context.ExpressionBuilder.Append(CultureInfo.InvariantCulture, $".WithName(\"{snapshotName}\")");
        return this;
    }

    /// <summary>Overrides path resolution with an explicit absolute or relative file path.</summary>
    /// <param name="filePath">The path to the expected baseline file.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    public RenderedSnapshotAssertion<T> AtPath(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        _explicitPath = filePath;
        Context.ExpressionBuilder.Append(CultureInfo.InvariantCulture, $".AtPath(\"{filePath}\")");
        return this;
    }

    /// <summary>Overrides the comparison options.</summary>
    /// <param name="options">The options to apply.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public RenderedSnapshotAssertion<T> WithOptions(SnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        Context.ExpressionBuilder.Append(".WithOptions(...)");
        return this;
    }

    /// <summary>Adds a <see cref="SnapshotScrubber"/> to the pipeline. Applied AFTER the
    /// renderer projects the value to a string; multiple <c>.WithScrubber()</c> calls
    /// compose left-to-right.</summary>
    /// <param name="scrubber">The scrubber to append.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scrubber"/> is <see langword="null"/>.</exception>
    public RenderedSnapshotAssertion<T> WithScrubber(SnapshotScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(scrubber);
        _scrubbers ??= [];
        _scrubbers.Add(scrubber);
        Context.ExpressionBuilder.Append(".WithScrubber(...)");
        return this;
    }

    /// <inheritdoc/>
    protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<T> metadata)
    {
        if (metadata.Exception is not null)
        {
            return Task.FromResult(AssertionResult.Failed(
                $"threw {metadata.Exception.GetType().Name}", metadata.Exception));
        }

        var value = metadata.Value;
        if (value is null)
            return Task.FromResult(AssertionResult.Failed("actual value was null"));

        string content;
        try
        {
            content = _renderer.Render(value);
        }
#pragma warning disable CA1031 // catch (Exception): renderer is consumer-supplied code; any throw must be surfaced as a failed assertion, not crash the test runner.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Task.FromResult(AssertionResult.Failed(
                $"renderer threw {ex.GetType().Name}: {ex.Message}", ex));
        }

        if (content is null)
            return Task.FromResult(AssertionResult.Failed("renderer returned null content"));

        var paths = SnapshotAssertionImpl.ResolvePaths(_explicitPath, _explicitName);
        return SnapshotAssertionImpl.EvaluateAsync(content, _scrubbers, _options, paths);
    }

    /// <inheritdoc/>
    protected override string GetExpectation() => "to match the snapshot baseline (rendered)";
}
