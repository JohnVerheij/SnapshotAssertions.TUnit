using System;

namespace SnapshotAssertions;

/// <summary>
/// Detection of accept-mode based on the <c>SNAPSHOT_ACCEPT</c> and <c>CI</c> environment
/// variables. Pure, no IO; the caller is responsible for actually overwriting the baseline
/// when accept-mode is active.
/// </summary>
/// <remarks>
/// <para>
/// Accept-mode is enabled when <c>SNAPSHOT_ACCEPT</c> is set to a truthy value (one of
/// <c>1</c>, <c>true</c>, or <c>yes</c>; case-insensitive). It is unconditionally
/// <em>disabled</em> in CI: if the <c>CI</c> environment variable is set to a truthy value
/// (which all major hosted CI services do: GitHub Actions, GitLab CI, Azure Pipelines,
/// CircleCI, etc.), accept-mode is refused even if <c>SNAPSHOT_ACCEPT</c> is set, to prevent
/// a stray pipeline configuration from silently accepting baseline drift.
/// </para>
/// <para>
/// The CI guard reads <c>CI</c> rather than the runner-specific variables (<c>GITHUB_ACTIONS</c>,
/// <c>GITLAB_CI</c>, <c>TF_BUILD</c>, etc.) because <c>CI</c> is the cross-runner canonical signal:
/// every major runner sets it. This keeps the rule one-line and runner-agnostic.
/// </para>
/// </remarks>
public static class SnapshotAcceptMode
{
    /// <summary>The environment variable consulted to enable accept-mode.</summary>
    public const string AcceptVariableName = "SNAPSHOT_ACCEPT";

    /// <summary>The environment variable consulted to detect a CI environment.</summary>
    public const string CiVariableName = "CI";

    /// <summary>
    /// Returns <see langword="true"/> when accept-mode is enabled and the process is not in CI.
    /// </summary>
    /// <returns><see langword="true"/> if the actual content should be written over the
    /// expected baseline on mismatch.</returns>
    public static bool IsActive()
        => IsActive(
            Environment.GetEnvironmentVariable(AcceptVariableName),
            Environment.GetEnvironmentVariable(CiVariableName));

    /// <summary>
    /// Pure overload for testability: classify the supplied raw values without touching the
    /// process environment.
    /// </summary>
    /// <param name="snapshotAcceptValue">The raw value of the <c>SNAPSHOT_ACCEPT</c>
    /// environment variable, or <see langword="null"/> if unset.</param>
    /// <param name="ciValue">The raw value of the <c>CI</c> environment variable, or
    /// <see langword="null"/> if unset.</param>
    /// <returns><see langword="true"/> if accept-mode is enabled AND the process is not in CI.</returns>
    public static bool IsActive(string? snapshotAcceptValue, string? ciValue)
        => IsTruthy(snapshotAcceptValue) && !IsTruthy(ciValue);

    /// <summary>Whether <paramref name="value"/> is one of the recognised truthy strings
    /// (<c>1</c>, <c>true</c>, <c>yes</c>; case-insensitive). <see langword="null"/> and empty
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
