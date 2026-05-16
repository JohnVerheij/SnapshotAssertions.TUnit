using System;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions.Render;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the framework-agnostic <see cref="SnapshotRenderer{T}"/> base class + the
/// <see cref="Renderer.For{T}(Func{T, string})"/> delegate factory. Adapter-side integration
/// of these renderers with <c>MatchesSnapshot</c> is exercised in the adapter test project.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class RendererTests
{
    /// <summary>Inline factory: returns a non-null renderer that invokes the supplied fn.</summary>
    [Test]
    public async Task For_DelegateFactory_RendersViaSuppliedFunction(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var renderer = Renderer.For<int>(x => $"value-{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        await Assert.That(renderer).IsNotNull();
        await Assert.That(renderer.Render(42)).IsEqualTo("value-42");
    }

    [Test]
    public async Task For_NullRenderFn_ThrowsArgumentNull(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => Renderer.For<int>(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task For_DelegateFactory_DistinctInputsProduceDistinctOutputs(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var renderer = Renderer.For<int>(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(renderer.Render(1)).IsEqualTo("1");
        await Assert.That(renderer.Render(2)).IsEqualTo("2");
    }

    /// <summary>Pins the protected default constructor on the base class via a subclass call.
    /// The base class is intentionally non-sealed (consumers subclass it).</summary>
    [Test]
    public async Task SnapshotRenderer_BaseClass_ProtectedConstructorAccessibleViaSubclass(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var subclass = new ExampleRenderer();
        await Assert.That(subclass.Render(99)).IsEqualTo("rendered-99");
    }

    /// <summary>Sample subclass anchor for the cookbook pattern: deterministic projection of
    /// a domain type to a canonical string.</summary>
    private sealed class ExampleRenderer : SnapshotRenderer<int>
    {
        public override string Render(int value) =>
            $"rendered-{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
