using System;

namespace SnapshotAssertions.Render;

/// <summary>
/// Static factories for <see cref="SnapshotRenderer{T}"/> instances. Use
/// <see cref="For{T}(System.Func{T, string})"/> to build a renderer from an inline lambda
/// when a full subclass is unnecessary.
/// </summary>
public static class Renderer
{
    /// <summary>
    /// Builds a <see cref="SnapshotRenderer{T}"/> that delegates to the supplied function.
    /// Equivalent to a one-line subclass of <see cref="SnapshotRenderer{T}"/>; use when the
    /// renderer is single-use and doesn't need its own type, configuration, or state.
    /// </summary>
    /// <typeparam name="T">The type to render.</typeparam>
    /// <param name="renderFn">The rendering function. Must not be <see langword="null"/>.</param>
    /// <returns>A renderer that invokes <paramref name="renderFn"/> on each
    /// <see cref="SnapshotRenderer{T}.Render(T)"/> call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="renderFn"/> is <see langword="null"/>.</exception>
    public static SnapshotRenderer<T> For<T>(Func<T, string> renderFn)
    {
        ArgumentNullException.ThrowIfNull(renderFn);
        return new DelegateRenderer<T>(renderFn);
    }

    private sealed class DelegateRenderer<T> : SnapshotRenderer<T>
    {
        private readonly Func<T, string> _renderFn;

        public DelegateRenderer(Func<T, string> renderFn)
        {
            _renderFn = renderFn;
        }

        public override string Render(T value) => _renderFn(value);
    }
}
