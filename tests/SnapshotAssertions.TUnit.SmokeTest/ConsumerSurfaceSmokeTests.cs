namespace Smoke.Consumer;

/// <summary>
/// Smoke tests proving that an external consumer can adopt SnapshotAssertions.TUnit purely via
/// the README's recommended GlobalUsings.cs snippet — no extra <c>using SnapshotAssertions.TUnit;</c>
/// directive at the test-file level, no other wiring. The test class lives in
/// <c>Smoke.Consumer</c> deliberately: SnapshotAssertions.TUnit's own test project is in the
/// <c>SnapshotAssertions.TUnit.Tests</c> namespace, which inherits parent-namespace visibility
/// into <c>SnapshotAssertions.TUnit</c>; that inheritance can mask resolution-pathway bugs.
/// By placing this file in a namespace with no parent relationship to SnapshotAssertions.TUnit,
/// this project is the canonical regression coverage for the resolution-pathway bug class.
/// </summary>
[Category("ConsumerSurface")]
[Timeout(10_000)]
internal sealed class ConsumerSurfaceSmokeTests
{
    /// <summary>Pins that the assembly's scaffold marker constant resolves cleanly for an
    /// external consumer using only the README's GlobalUsings snippet. Replaced by real
    /// entry-point smoke tests once the 0.1.0 public API ships.</summary>
    [Test]
    public async Task ScaffoldMarkerResolvesForExternalConsumer(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAssertionsTUnitInfo.ScaffoldMarker)
            .IsEqualTo("snapshot-assertions-tunit-0.1.0-scaffold");
    }

    /// <summary>Pins that <see cref="SnapshotOptions.Default"/> resolves cleanly for an
    /// external consumer. Validates that the framework-agnostic core is reachable from the
    /// adapter package's transitive surface.</summary>
    [Test]
    public async Task SnapshotOptionsDefaultResolvesForExternalConsumer(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default;

        await Assert.That(options.LineEndingMode).IsEqualTo(SnapshotLineEndingMode.Ordinal);
    }
}
