namespace Smoke.Consumer;

/// <summary>
/// Pins what the packed build targets record for accept mode: the runtime host configuration option
/// <c>SnapshotAssertions.ProjectDirectory</c>. The smoke project writes its build output outside its own
/// folder (see its <c>OutputPath</c>), the layout where the old walk-up from the output directory could not find
/// the project. Accept mode itself cannot run here: it needs <c>SNAPSHOT_ACCEPT</c> in the process environment
/// (not settable without leaking into parallel tests) and is refused when <c>CI</c> is set, so this test
/// asserts the option the accept path reads, in the managed run and in the Native AOT run alike, and the failure
/// message line that names the baseline in the project's <c>Snapshots/</c> folder.
/// </summary>
[Category("ConsumerSurface")]
[Timeout(15_000)]
internal sealed class ProjectDirectorySmokeTests
{
    /// <summary>The key the package's build targets set; the adapter reads it through
    /// <see cref="AppContext.GetData(string)"/>.</summary>
    private const string ProjectDirectoryKey = "SnapshotAssertions.ProjectDirectory";

    /// <summary>The build targets record the consuming project's directory, which holds the project file.</summary>
    [Test]
    public async Task BuildTargets_RecordTheProjectDirectory(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectDirectory = AppContext.GetData(ProjectDirectoryKey) as string;

        await Assert.That(projectDirectory).IsNotNull();
        await Assert.That(Directory.Exists(projectDirectory)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(projectDirectory!, "SnapshotAssertions.TUnit.SmokeTest.csproj"))).IsTrue();
    }

    /// <summary>The build output is outside the recorded project directory, so the option is what lets accept
    /// mode find the project's <c>Snapshots/</c> folder.</summary>
    [Test]
    public async Task BuildOutput_IsOutsideTheRecordedProjectDirectory(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath((AppContext.GetData(ProjectDirectoryKey) as string)!));
        var outputDirectory = Path.GetFullPath(AppContext.BaseDirectory);

        // Compare with a trailing separator, so a sibling folder whose name starts with the project's is not inside it.
        await Assert.That(outputDirectory.StartsWith(projectDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    /// <summary>A <c>.WithName(...)</c> snapshot without a baseline fails with a message that names the baseline's path
    /// in the recorded project's <c>Snapshots/</c> folder, for the string and the renderer assertion alike. Skipped
    /// when accept mode is on, because a missing baseline is then written into the project instead of failing.</summary>
    [Test]
    public async Task FailureMessage_NamesTheProjectBaseline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Skip.When(SnapshotAssertions.SnapshotAcceptMode.IsActive(), "Accept mode would write the baseline into the project.");
        var projectDirectory = Path.GetFullPath((AppContext.GetData(ProjectDirectoryKey) as string)!);
        var name = "ProjectBaselineSmoke_" + Guid.NewGuid().ToString("N");
        var expectedLine = "Project baseline: " + Path.Combine(projectDirectory, "Snapshots", name + ".expected.txt");
        var actualPath = Path.Combine(AppContext.BaseDirectory, "Snapshots", name + ".actual.txt");
        var content = "no baseline\n";
        var value = 1;
        try
        {
            var stringFailure = await Assert.That(async () => await Assert.That(content).MatchesSnapshot().WithName(name))
                .Throws<TUnit.Assertions.Exceptions.AssertionException>();
            var rendererFailure = await Assert.That(async () => await Assert.That(value).MatchesSnapshot(static _ => "value\n").WithName(name))
                .Throws<TUnit.Assertions.Exceptions.AssertionException>();

            await Assert.That(stringFailure!.Message).Contains(expectedLine);
            await Assert.That(rendererFailure!.Message).Contains(expectedLine);
        }
        finally
        {
            File.Delete(actualPath);
        }
    }
}
