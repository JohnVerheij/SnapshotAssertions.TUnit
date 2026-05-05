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
[Timeout(15_000)]
internal sealed class ConsumerSurfaceSmokeTests
{
    /// <summary>Pins that the source-generated <c>MatchesSnapshot()</c> entry point resolves
    /// for an external consumer using only the README's GlobalUsings snippet.</summary>
    [Test]
    public async Task EntryPointMatchesSnapshotResolvesAndPasses(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "smoke-entry.expected.txt");
        await File.WriteAllTextAsync(expected, "smoke\n", cancellationToken).ConfigureAwait(false);

        await Assert.That("smoke\n").MatchesSnapshot().AtPath(expected);
    }

    /// <summary>Pins that the <c>MatchesSnapshotFile(string)</c> shorthand extension resolves
    /// for an external consumer.</summary>
    [Test]
    public async Task ShorthandMatchesSnapshotFileResolvesAndPasses(CancellationToken cancellationToken)
    {
        var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "smoke-shorthand.expected.txt");
        await File.WriteAllTextAsync(expected, "shorthand\n", cancellationToken).ConfigureAwait(false);

        await Assert.That("shorthand\n").MatchesSnapshotFile(expected);
    }

    /// <summary>Pins that <see cref="SnapshotOptions.Default"/> resolves for an external
    /// consumer. Validates that the framework-agnostic core is reachable transitively.</summary>
    [Test]
    public async Task SnapshotOptionsDefaultResolvesForExternalConsumer(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default;
        await Assert.That(options.LineEndingMode).IsEqualTo(SnapshotLineEndingMode.Ordinal);
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smoke-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
