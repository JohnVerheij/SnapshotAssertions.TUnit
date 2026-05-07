using System.Threading;
using System.Threading.Tasks;
using PublicApiGenerator;
using SnapshotAssertions;

namespace SnapshotAssertions.TUnit.SnapshotTests;

/// <summary>
/// Pins the public API surface of both shipped packages (<c>SnapshotAssertions</c> and
/// <c>SnapshotAssertions.TUnit</c>) using the package's <i>own</i> <c>MatchesSnapshot()</c>
/// chain. Any change to a public type, member, signature, attribute, or visibility produces
/// a diff against the corresponding <c>.expected.txt</c> file under <c>Snapshots/</c> and
/// fails the test until the snapshot is explicitly re-accepted (write the new content to the
/// expected path, or run with <c>SNAPSHOT_ACCEPT=1</c> to auto-write).
/// </summary>
/// <remarks>
/// <para>
/// Dogfooding the package on its own public surface: the snapshot tests use the same
/// <c>MatchesSnapshot()</c> call site that downstream consumers use, with the same
/// <c>Snapshots/{TestClass}.{TestMethod}.expected.txt</c> file convention. If the snapshot
/// engine ever regresses for a real consumer, this project's tests will surface that
/// regression on the package's own CI before the regression reaches a downstream user.
/// </para>
/// <para>
/// Stronger than ApiCompat's per-version baseline check because these snapshots fire on
/// every PR, not just at pack time.
/// </para>
/// </remarks>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class PublicApiTests
{
    /// <summary>
    /// Pins the public surface of the framework-agnostic <c>SnapshotAssertions</c> assembly:
    /// <c>SnapshotComparer</c>, <c>SnapshotEvaluator</c>, <c>SnapshotFileResolver</c>,
    /// <c>SnapshotAcceptMode</c>, <c>SnapshotOptions</c> and the four enum types,
    /// <c>SnapshotResult</c> + <c>SnapshotMatchOutcome</c>, <c>SnapshotPaths</c>,
    /// <c>SnapshotException</c>, <c>LineDiffRenderer</c>, and v0.2.0's <c>SnapshotScrubber</c>
    /// + <c>SnapshotScrubberState</c> + <c>Scrubbers</c> factory.
    /// </summary>
    [Test]
    public async Task SnapshotAssertionsPublicApiHasNotChangedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var assembly = typeof(SnapshotComparer).Assembly;
        var publicApi = assembly.GeneratePublicApi();

        await Assert.That(publicApi).MatchesSnapshot();
    }

    /// <summary>
    /// Pins the public surface of the TUnit adapter assembly: the <c>SnapshotAssertion</c>
    /// class with all its chain methods (<c>WithName</c>, <c>AtPath</c>, <c>WithOptions</c>,
    /// and v0.2.0's <c>WithScrubber</c>), the <c>MatchesSnapshot</c> source-generator entry
    /// point, and the shorthand <c>MatchesSnapshotShorthandExtensions</c>.
    /// </summary>
    [Test]
    public async Task SnapshotAssertionsTUnitPublicApiHasNotChangedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var assembly = typeof(SnapshotAssertion).Assembly;
        var publicApi = assembly.GeneratePublicApi();

        await Assert.That(publicApi).MatchesSnapshot();
    }
}
