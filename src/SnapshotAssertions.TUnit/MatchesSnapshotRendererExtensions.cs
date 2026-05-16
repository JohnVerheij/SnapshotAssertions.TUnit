using System;
using SnapshotAssertions.Render;
using SnapshotAssertions.TUnit;
using TUnit.Assertions.Core;

namespace TUnit.Assertions.Extensions;

/// <summary>
/// Renderer-projected entry points for <c>MatchesSnapshot</c>. Two overloads cover the common
/// shapes for projecting an arbitrary source type to a canonical string before snapshot
/// comparison: a polymorphic overload taking a <see cref="SnapshotRenderer{T}"/> subclass
/// (configurable, stateful renderers) and a delegate-shaped overload taking a
/// <see cref="Func{T, TResult}"/> (single-use inline projections; also the shape used by
/// sibling family packages that publish renderers as static helper methods without taking a
/// reference on <c>SnapshotAssertions</c>).
/// </summary>
public static class MatchesSnapshotRendererExtensions
{
    /// <summary>
    /// Asserts that <paramref name="source"/>'s actual value, after canonical-string rendering
    /// via <paramref name="renderer"/>, matches the snapshot baseline. Use this overload when
    /// the renderer is a reusable subclass that owns configuration or state.
    /// </summary>
    /// <typeparam name="T">The source value type.</typeparam>
    /// <param name="source">The assertion source over <typeparamref name="T"/>.</param>
    /// <param name="renderer">The renderer projecting <typeparamref name="T"/> to a canonical
    /// string. Must not be <see langword="null"/>.</param>
    /// <returns>The renderer-projected snapshot assertion, ready for further chaining
    /// (<c>.WithName</c>, <c>.AtPath</c>, <c>.WithOptions</c>, <c>.WithScrubber</c>).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is <see langword="null"/>.</exception>
    public static RenderedSnapshotAssertion<T> MatchesSnapshot<T>(
        this IAssertionSource<T> source,
        SnapshotRenderer<T> renderer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(renderer);
        source.Context.ExpressionBuilder.Append(".MatchesSnapshot(renderer)");
        return new RenderedSnapshotAssertion<T>(source.Context, renderer);
    }

    /// <summary>
    /// Asserts that <paramref name="source"/>'s actual value, after canonical-string rendering
    /// via <paramref name="render"/>, matches the snapshot baseline. Use this overload for
    /// single-use inline projections (e.g. <c>obj =&gt; obj.ToCanonicalString()</c>) or when
    /// composing with a sibling family package's static renderer method without taking a
    /// reference on its assembly.
    /// </summary>
    /// <typeparam name="T">The source value type.</typeparam>
    /// <param name="source">The assertion source over <typeparamref name="T"/>.</param>
    /// <param name="render">The rendering function. Must not be <see langword="null"/>.</param>
    /// <returns>The renderer-projected snapshot assertion, ready for further chaining
    /// (<c>.WithName</c>, <c>.AtPath</c>, <c>.WithOptions</c>, <c>.WithScrubber</c>).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="render"/> is <see langword="null"/>.</exception>
    public static RenderedSnapshotAssertion<T> MatchesSnapshot<T>(
        this IAssertionSource<T> source,
        Func<T, string> render)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(render);
        source.Context.ExpressionBuilder.Append(".MatchesSnapshot(render)");
        return new RenderedSnapshotAssertion<T>(source.Context, Renderer.For(render));
    }
}
