using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the env-var truthy detection and the CI-guard interaction in
/// <see cref="SnapshotAcceptMode"/>. The CI guard is security-critical: a stray pipeline
/// configuration must never silently overwrite a baseline with the test's actual output.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotAcceptModeTests
{
    /// <summary>Recognised truthy strings activate accept-mode.</summary>
    [Test]
    [Arguments("1")]
    [Arguments("true")]
    [Arguments("TRUE")]
    [Arguments("True")]
    [Arguments("yes")]
    [Arguments("Yes")]
    [Arguments(" 1 ")]
    public async Task TruthyStrings_AreRecognised(string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.IsTruthy(value)).IsTrue();
    }

    /// <summary>Falsy / missing values do not activate accept-mode.</summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("0")]
    [Arguments("false")]
    [Arguments("no")]
    [Arguments("anything-else")]
    public async Task NonTruthyStrings_AreFalsy(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.IsTruthy(value)).IsFalse();
    }

    /// <summary>Accept-mode active: SNAPSHOT_ACCEPT truthy and CI not set.</summary>
    [Test]
    public async Task AcceptSet_AndCiUnset_IsActive(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.IsActive("1", null)).IsTrue();
    }

    /// <summary>CI guard: when CI is truthy, accept-mode is refused even if SNAPSHOT_ACCEPT
    /// is also truthy. This is the load-bearing rule that prevents pipeline-side baseline
    /// drift.</summary>
    [Test]
    public async Task AcceptSet_AndCiSet_IsInactive(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.IsActive("1", "true")).IsFalse();
    }

    /// <summary>Accept-mode unset: regardless of CI flag, accept-mode is inactive.</summary>
    [Test]
    public async Task AcceptUnset_IsAlwaysInactive(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.IsActive(null, null)).IsFalse();
        await Assert.That(SnapshotAcceptMode.IsActive(null, "true")).IsFalse();
        await Assert.That(SnapshotAcceptMode.IsActive("0", null)).IsFalse();
    }
}
