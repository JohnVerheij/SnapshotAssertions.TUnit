using System;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>Hooks that run once for the whole test session.</summary>
internal static class TestSessionHooks
{
    /// <summary>
    /// Clears the accept-mode variables before any test runs, so a developer's <c>SNAPSHOT_ACCEPT=1</c>, set to accept
    /// baselines elsewhere, cannot turn the failures these tests expect into accepted baselines. A test that needs
    /// accept mode passes it explicitly or sets the variables itself, alone (<c>[NotInParallel]</c>).
    /// </summary>
    [Before(TestSession)]
    public static void ClearAcceptModeVariables()
    {
        Environment.SetEnvironmentVariable(SnapshotAcceptMode.AcceptVariableName, null);
        Environment.SetEnvironmentVariable(SnapshotAcceptMode.AllowInCiVariableName, null);
    }
}
