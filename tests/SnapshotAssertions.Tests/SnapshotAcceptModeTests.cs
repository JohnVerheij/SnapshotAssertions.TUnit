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
    /// <summary>Recognized truthy strings activate accept-mode.</summary>
    [Test]
    [Arguments("1")]
    [Arguments("true")]
    [Arguments("TRUE")]
    [Arguments("True")]
    [Arguments("yes")]
    [Arguments("Yes")]
    [Arguments(" 1 ")]
    public async Task TruthyStrings_AreRecognized(string value, CancellationToken cancellationToken)
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

    /// <summary>Truth table of the four-variable form: accept, CI, TF_BUILD and allow-in-CI.</summary>
    [Test]
    [Arguments(null, null, null, null, false, DisplayName = "Four_AcceptUnset")]
    [Arguments("0", null, null, null, false, DisplayName = "Four_AcceptZero")]
    [Arguments("false", null, null, "1", false, DisplayName = "Four_AcceptFalseWithAllow")]
    [Arguments(null, null, null, "1", false, DisplayName = "Four_AcceptUnsetWithAllow")]
    [Arguments("1", null, null, null, true, DisplayName = "Four_NoCi")]
    [Arguments("yes", "", null, "", true, DisplayName = "Four_EmptyValuesAreNotCi")]
    [Arguments("1", "false", "False", null, true, DisplayName = "Four_FalseValuesAreNotCi")]
    [Arguments("1", "0", "0", null, true, DisplayName = "Four_ZeroValuesAreNotCi")]
    [Arguments("1", "  ", "  ", null, true, DisplayName = "Four_WhitespaceValuesAreNotCi")]
    [Arguments("1", "true", null, null, false, DisplayName = "Four_CiTrue")]
    [Arguments("1", "1", null, null, false, DisplayName = "Four_CiOne")]
    [Arguments("1", null, "True", null, false, DisplayName = "Four_TfBuildTrueCased")]
    [Arguments("1", null, "true", null, false, DisplayName = "Four_TfBuildLower")]
    [Arguments("1", "true", "True", null, false, DisplayName = "Four_CiAndTfBuild")]
    [Arguments("1", "true", null, "1", true, DisplayName = "Four_AllowOverridesCi")]
    [Arguments("1", null, "True", "true", true, DisplayName = "Four_AllowOverridesTfBuild")]
    [Arguments("1", "true", "True", "yes", true, DisplayName = "Four_AllowOverridesBoth")]
    [Arguments("1", "true", "True", "false", false, DisplayName = "Four_AllowFalseDoesNotOverride")]
    [Arguments("1", "true", "True", " ", false, DisplayName = "Four_AllowWhitespaceDoesNotOverride")]
    [Arguments("1", null, null, "1", true, DisplayName = "Four_AllowWithoutCi")]
    public async Task FourVariableForm_FollowsTheTruthTable(
        string? accept, string? ci, string? azurePipelines, string? allowInCi, bool expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.IsActive(accept, ci, azurePipelines, allowInCi)).IsEqualTo(expected);
    }

    /// <summary>The two-variable form keeps its meaning: only SNAPSHOT_ACCEPT and CI decide, and it agrees with
    /// the four-variable form when TF_BUILD and SNAPSHOT_ACCEPT_IN_CI are unset.</summary>
    [Test]
    [Arguments("1", null, true, DisplayName = "Two_NoCi")]
    [Arguments("1", "", true, DisplayName = "Two_EmptyCi")]
    [Arguments("1", "false", true, DisplayName = "Two_FalseCi")]
    [Arguments("1", "true", false, DisplayName = "Two_CiTrue")]
    [Arguments("1", "yes", false, DisplayName = "Two_CiYes")]
    [Arguments(null, null, false, DisplayName = "Two_AcceptUnset")]
    [Arguments("0", "true", false, DisplayName = "Two_AcceptZeroCi")]
    public async Task TwoVariableForm_IsUnchanged(string? accept, string? ci, bool expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.IsActive(accept, ci)).IsEqualTo(expected);
        await Assert.That(SnapshotAcceptMode.IsActive(accept, ci, null, null)).IsEqualTo(expected);
    }

    /// <summary>The two-variable form ignores TF_BUILD and SNAPSHOT_ACCEPT_IN_CI, which it has no parameters
    /// for, while the four-variable form honors them.</summary>
    [Test]
    public async Task TwoVariableForm_DoesNotSeeTheNewVariables(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.IsActive("1", "true")).IsFalse();
        await Assert.That(SnapshotAcceptMode.IsActive("1", "true", null, "1")).IsTrue();
        await Assert.That(SnapshotAcceptMode.IsActive("1", null)).IsTrue();
        await Assert.That(SnapshotAcceptMode.IsActive("1", null, "True", null)).IsFalse();
    }

    /// <summary>The variable-name constants carry the documented names.</summary>
    [Test]
    public async Task VariableNames_AreTheDocumentedOnes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(SnapshotAcceptMode.AcceptVariableName).IsEqualTo("SNAPSHOT_ACCEPT");
        await Assert.That(SnapshotAcceptMode.CiVariableName).IsEqualTo("CI");
        await Assert.That(SnapshotAcceptMode.AzurePipelinesVariableName).IsEqualTo("TF_BUILD");
        await Assert.That(SnapshotAcceptMode.AllowInCiVariableName).IsEqualTo("SNAPSHOT_ACCEPT_IN_CI");
    }
}
