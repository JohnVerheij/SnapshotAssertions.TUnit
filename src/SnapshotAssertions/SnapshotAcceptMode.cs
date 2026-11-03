using System;

namespace SnapshotAssertions;

/// <summary>
/// Detection of accept-mode based on the <c>SNAPSHOT_ACCEPT</c>, <c>CI</c>, <c>TF_BUILD</c> and
/// <c>SNAPSHOT_ACCEPT_IN_CI</c> environment variables. Pure, no IO; the caller is responsible for
/// actually overwriting the baseline when accept-mode is active.
/// </summary>
/// <remarks>
/// <para>
/// Accept-mode is enabled when <c>SNAPSHOT_ACCEPT</c> is set to a truthy value (one of
/// <c>1</c>, <see langword="true"/>, or <c>yes</c>; case-insensitive). It is
/// <em>disabled</em> in CI: accept-mode is then refused even if <c>SNAPSHOT_ACCEPT</c> is set, to prevent a
/// stray pipeline configuration from silently accepting baseline drift. CI is detected through <c>CI</c>
/// (GitHub Actions, GitLab CI and most other systems set <c>CI=true</c>) and <c>TF_BUILD</c> (Azure Pipelines
/// sets <c>TF_BUILD=True</c>); either one set to a truthy value counts as CI.
/// </para>
/// <para>
/// A pipeline step that regenerates baselines on purpose sets <c>SNAPSHOT_ACCEPT_IN_CI=1</c> next to
/// <c>SNAPSHOT_ACCEPT=1</c>; a truthy <c>SNAPSHOT_ACCEPT_IN_CI</c> lifts the CI guard. Setting <c>CI</c> to a
/// non-truthy value for the step still works where only <c>CI</c> is set, but not on Azure Pipelines, where
/// <c>TF_BUILD</c> is set as well.
/// </para>
/// </remarks>
public static class SnapshotAcceptMode
{
    /// <summary>The environment variable consulted to enable accept-mode.</summary>
    public const string AcceptVariableName = "SNAPSHOT_ACCEPT";

    /// <summary>The environment variable consulted to detect a CI environment (most CI systems set it).</summary>
    public const string CiVariableName = "CI";

    /// <summary>The environment variable consulted to detect Azure Pipelines, which sets it to <c>True</c>.</summary>
    public const string AzurePipelinesVariableName = "TF_BUILD";

    /// <summary>The environment variable that lifts the CI guard when it is truthy, for a pipeline step that
    /// regenerates baselines on purpose.</summary>
    public const string AllowInCiVariableName = "SNAPSHOT_ACCEPT_IN_CI";

    /// <summary>Returns <see langword="true"/> when accept-mode is enabled and either the process is not in CI
    /// or <c>SNAPSHOT_ACCEPT_IN_CI</c> lifts the CI guard. Reads <c>SNAPSHOT_ACCEPT</c>, <c>CI</c>,
    /// <c>TF_BUILD</c> and <c>SNAPSHOT_ACCEPT_IN_CI</c>.</summary>
    /// <returns><see langword="true"/> if the actual content should be written over the
    /// expected baseline on mismatch.</returns>
    public static bool IsActive()
        => IsActive(
            Environment.GetEnvironmentVariable(AcceptVariableName),
            Environment.GetEnvironmentVariable(CiVariableName),
            Environment.GetEnvironmentVariable(AzurePipelinesVariableName),
            Environment.GetEnvironmentVariable(AllowInCiVariableName));

    /// <summary>
    /// Pure two-variable overload: classify the supplied raw values without touching the process
    /// environment, looking only at <c>SNAPSHOT_ACCEPT</c> and <c>CI</c>. It ignores <c>TF_BUILD</c> and
    /// <c>SNAPSHOT_ACCEPT_IN_CI</c>; see <see cref="IsActive(string?, string?, string?, string?)"/> for the
    /// form that <see cref="IsActive()"/> uses.
    /// </summary>
    /// <param name="snapshotAcceptValue">The raw value of the <c>SNAPSHOT_ACCEPT</c>
    /// environment variable, or <see langword="null"/> if unset.</param>
    /// <param name="ciValue">The raw value of the <c>CI</c> environment variable, or
    /// <see langword="null"/> if unset.</param>
    /// <returns><see langword="true"/> if accept-mode is enabled AND the process is not in CI.</returns>
    public static bool IsActive(string? snapshotAcceptValue, string? ciValue)
        => IsTruthy(snapshotAcceptValue) && !IsTruthy(ciValue);

    /// <summary>
    /// Pure four-variable overload: classify the supplied raw values without touching the process
    /// environment. Accept-mode is active when <paramref name="snapshotAcceptValue"/> is truthy and either
    /// <paramref name="allowInCiValue"/> is truthy or neither <paramref name="ciValue"/> nor
    /// <paramref name="azurePipelinesValue"/> is truthy.
    /// </summary>
    /// <param name="snapshotAcceptValue">The raw value of the <c>SNAPSHOT_ACCEPT</c>
    /// environment variable, or <see langword="null"/> if unset.</param>
    /// <param name="ciValue">The raw value of the <c>CI</c> environment variable, or
    /// <see langword="null"/> if unset.</param>
    /// <param name="azurePipelinesValue">The raw value of the <c>TF_BUILD</c> environment variable (Azure
    /// Pipelines sets <c>True</c>), or <see langword="null"/> if unset.</param>
    /// <param name="allowInCiValue">The raw value of the <c>SNAPSHOT_ACCEPT_IN_CI</c> environment variable, or
    /// <see langword="null"/> if unset.</param>
    /// <returns><see langword="true"/> if accept-mode is enabled AND the CI guard does not apply.</returns>
    public static bool IsActive(string? snapshotAcceptValue, string? ciValue, string? azurePipelinesValue, string? allowInCiValue)
        => IsTruthy(snapshotAcceptValue)
            && (IsTruthy(allowInCiValue) || !(IsTruthy(ciValue) || IsTruthy(azurePipelinesValue)));

    /// <summary>Whether <paramref name="value"/> is one of the recognised truthy strings
    /// (<c>1</c>, <see langword="true"/>, <c>yes</c>; case-insensitive). <see langword="null"/> and empty
    /// strings are falsy.</summary>
    /// <param name="value">The value to classify.</param>
    /// <returns><see langword="true"/> if truthy.</returns>
    public static bool IsTruthy(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var trimmed = value.Trim();
        return string.Equals(trimmed, "1", StringComparison.Ordinal)
            || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
