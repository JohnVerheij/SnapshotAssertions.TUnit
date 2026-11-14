using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;
using TUnit.Assertions.Exceptions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Drives accept mode the way a user does: the four environment variables are set in the real process and a real
/// <c>MatchesSnapshot()</c> assertion runs, rather than calling <see cref="SnapshotAcceptMode"/> in isolation. The
/// tests change process-wide state, so each one runs alone (<c>[NotInParallel]</c> without a key waits for every
/// other test) and restores the variables afterwards, including when the assertion throws.
/// </summary>
[NotInParallel]
[Timeout(15_000)]
internal sealed class AcceptModeEndToEndTests
{
    private static readonly string[] Variables =
    [
        SnapshotAcceptMode.AcceptVariableName,
        SnapshotAcceptMode.CiVariableName,
        SnapshotAcceptMode.AzurePipelinesVariableName,
        SnapshotAcceptMode.AllowInCiVariableName,
    ];

    private readonly Dictionary<string, string?> _saved = new(StringComparer.Ordinal);

    /// <summary>Remembers the current values, so the test body can set any combination.</summary>
    [Before(Test)]
    public void SaveEnvironment(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var name in Variables)
            _saved[name] = Environment.GetEnvironmentVariable(name);
    }

    /// <summary>Restores the variables the test changed.</summary>
    [After(Test)]
    public void RestoreEnvironment(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var name in Variables)
            Environment.SetEnvironmentVariable(name, _saved[name]);
    }

    /// <summary>
    /// The decision table through the adapter: a missing baseline and a mismatching baseline are both accepted
    /// exactly when <see cref="SnapshotAcceptMode"/> says so, including mixed case and padded values. A refused
    /// accept fails the assertion and leaves the baseline untouched.
    /// </summary>
    [Test]
    [Arguments(null, null, null, null, false, DisplayName = "EndToEnd_AcceptUnset")]
    [Arguments("1", null, null, null, true, DisplayName = "EndToEnd_AcceptOne")]
    [Arguments(" True ", null, null, null, true, DisplayName = "EndToEnd_AcceptPaddedMixedCase")]
    [Arguments("YES", null, null, null, true, DisplayName = "EndToEnd_AcceptYesUpper")]
    [Arguments("0", null, null, null, false, DisplayName = "EndToEnd_AcceptZero")]
    [Arguments("1", "true", null, null, false, DisplayName = "EndToEnd_CiTrueRefuses")]
    [Arguments("1", "TRUE", null, null, false, DisplayName = "EndToEnd_CiUpperRefuses")]
    [Arguments("1", null, "True", null, false, DisplayName = "EndToEnd_TfBuildRefuses")]
    [Arguments("1", "false", null, null, true, DisplayName = "EndToEnd_CiFalseAllows")]
    [Arguments("1", "true", "True", "1", true, DisplayName = "EndToEnd_AllowInCiLiftsGuard")]
    [Arguments("1", "true", "True", " yes ", true, DisplayName = "EndToEnd_AllowInCiPaddedLiftsGuard")]
    [Arguments("1", "true", null, "0", false, DisplayName = "EndToEnd_AllowInCiZeroKeepsGuard")]
    [Arguments(null, "true", "True", "1", false, DisplayName = "EndToEnd_AllowWithoutAcceptStaysOff")]
    public async Task DecisionTable_MissingAndMismatchingBaseline(
        string? accept, string? ci, string? tfBuild, string? allowInCi, bool accepted, CancellationToken cancellationToken)
    {
        var directory = CreateTempDirectory();
        try
        {
            SetEnvironment(accept, ci, tfBuild, allowInCi);
            var expected = Path.Combine(directory, "table.expected.txt");

            // Missing baseline.
            await RunAsync(accepted, expected, "first\n");
            if (accepted)
                await Assert.That(await File.ReadAllTextAsync(expected, cancellationToken)).IsEqualTo("first\n");
            else
                await Assert.That(File.Exists(expected)).IsFalse();

            // Mismatching baseline.
            await File.WriteAllTextAsync(expected, "old\n", cancellationToken);
            await RunAsync(accepted, expected, "new\n");
            await Assert.That(await File.ReadAllTextAsync(expected, cancellationToken)).IsEqualTo(accepted ? "new\n" : "old\n");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Accepted baselines hold the scrubbed text, so a re-run without accept mode matches.</summary>
    [Test]
    public async Task Accept_WritesScrubbedText_AndTheNextRunMatches(CancellationToken cancellationToken)
    {
        var directory = CreateTempDirectory();
        try
        {
            var expected = Path.Combine(directory, "scrubbed.expected.txt");
            var content = "id=3f2504e0-4f89-11d3-9a0c-0305e82c3301 at 2024-01-01T10:00:00Z\n";

            SetEnvironment("1", null, null, null);
            await Assert.That(content).MatchesSnapshot().AtPath(expected).WithScrubber(Scrubbers.Default);
            var accepted = await File.ReadAllTextAsync(expected, cancellationToken);
            await Assert.That(accepted).DoesNotContain("3f2504e0");
            await Assert.That(accepted).DoesNotContain("2024-01-01");

            SetEnvironment(null, null, null, null);
            await Assert.That("id=11111111-2222-3333-4444-555555555555 at 2030-05-05T01:02:03Z\n")
                .MatchesSnapshot().AtPath(expected).WithScrubber(Scrubbers.Default);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task RunAsync(bool expectPass, string expectedPath, string content)
    {
        if (expectPass)
        {
            await Assert.That(content).MatchesSnapshot().AtPath(expectedPath);
            return;
        }

        await Assert.That(async () => await Assert.That(content).MatchesSnapshot().AtPath(expectedPath))
            .Throws<AssertionException>();
    }

    private static void SetEnvironment(string? accept, string? ci, string? tfBuild, string? allowInCi)
    {
        Environment.SetEnvironmentVariable(SnapshotAcceptMode.AcceptVariableName, accept);
        Environment.SetEnvironmentVariable(SnapshotAcceptMode.CiVariableName, ci);
        Environment.SetEnvironmentVariable(SnapshotAcceptMode.AzurePipelinesVariableName, tfBuild);
        Environment.SetEnvironmentVariable(SnapshotAcceptMode.AllowInCiVariableName, allowInCi);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "accept-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
