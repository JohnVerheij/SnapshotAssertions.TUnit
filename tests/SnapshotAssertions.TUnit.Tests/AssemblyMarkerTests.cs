using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions.TUnit;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins that the SnapshotAssertions.TUnit assembly loads cleanly and exposes its
/// scaffold-marker constant. Replaced once the real public API (<c>MatchesSnapshot</c>
/// entry points) ships.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class AssemblyMarkerTests
{
    /// <summary>The assembly's scaffold marker constant is reachable.</summary>
    [Test]
    public async Task ScaffoldMarkerIsReachable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAssertionsTUnitInfo.ScaffoldMarker)
            .IsEqualTo("snapshot-assertions-tunit-0.1.0-scaffold");
    }
}
