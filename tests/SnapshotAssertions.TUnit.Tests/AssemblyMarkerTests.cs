using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions.TUnit;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins that the SnapshotAssertions.TUnit assembly loads cleanly and that the
/// <see cref="SnapshotAssertion"/> public type is reachable. Most adapter behaviour is
/// exercised in <see cref="MatchesSnapshotTests"/>; this file is a basic load-time check.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class AssemblyMarkerTests
{
    /// <summary>The assertion type loads from the adapter assembly.</summary>
    [Test]
    public async Task AssertionTypeIsReachable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(typeof(SnapshotAssertion).FullName).IsEqualTo("SnapshotAssertions.TUnit.SnapshotAssertion");
    }
}
