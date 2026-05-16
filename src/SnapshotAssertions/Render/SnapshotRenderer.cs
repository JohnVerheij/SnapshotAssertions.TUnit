namespace SnapshotAssertions.Render;

/// <summary>
/// Renders an instance of <typeparamref name="T"/> as a canonical string for snapshot
/// comparison. Consumers subclass this to plug their domain types (Google.Protobuf messages,
/// <c>XDocument</c>, <c>Activity</c>, project-specific value objects) into the snapshot
/// pipeline without the framework-agnostic core taking on those types as dependencies.
/// </summary>
/// <typeparam name="T">The type to render.</typeparam>
/// <remarks>
/// <para>The renderer should produce <strong>deterministic</strong> output for a given input.
/// Non-determinism (random ordering, embedded timestamps, environment-specific paths) in the
/// rendered string defeats the purpose of snapshot testing; if such fields exist on
/// <typeparamref name="T"/>, prefer to either omit them in the renderer or rely on the
/// snapshot-pipeline <see cref="SnapshotScrubber"/> chain to remove them after rendering.</para>
/// <para>Inline renderers (single-test, ad-hoc) can be built from a delegate via
/// <see cref="Renderer.For{T}(System.Func{T, string})"/> without subclassing.</para>
/// <para>Sibling family packages (e.g. <c>LogAssertions.TUnit</c>) can publish renderers
/// for their own types as static helper methods that match the delegate-overload of
/// <c>MatchesSnapshot</c> directly, without taking a reference on
/// <c>SnapshotAssertions</c>. Subclassing this base type is the opt-in path for renderers
/// that need configuration / state / inheritance.</para>
/// </remarks>
public abstract class SnapshotRenderer<T>
{
    /// <summary>Initialises a new renderer instance.</summary>
    protected SnapshotRenderer() { }

    /// <summary>Renders <paramref name="value"/> as a canonical string suitable for snapshot
    /// comparison.</summary>
    /// <param name="value">The value to render. Implementations should document their
    /// behaviour for <see langword="null"/> inputs (typically: throw <see cref="System.ArgumentNullException"/>
    /// or return a sentinel like <c>"&lt;null&gt;"</c>).</param>
    /// <returns>The canonical string rendering.</returns>
    public abstract string Render(T value);
}
