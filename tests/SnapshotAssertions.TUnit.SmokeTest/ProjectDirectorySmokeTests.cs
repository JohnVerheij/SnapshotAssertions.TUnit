namespace Smoke.Consumer;

/// <summary>
/// Pins what the packed build targets record for accept mode: the runtime host configuration option
/// <c>SnapshotAssertions.ProjectDirectory</c>. The smoke project writes its build output outside its own
/// folder (see its <c>OutputPath</c>), the layout where the old walk-up from the output directory could not find
/// the project. Accept mode itself cannot run here: it needs <c>SNAPSHOT_ACCEPT</c> in the process environment
/// (not settable without leaking into parallel tests) and is refused when <c>CI</c> is set, so this test
/// asserts the option the accept path reads, in the managed run and in the Native AOT run alike.
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
        var projectDirectory = Path.GetFullPath((AppContext.GetData(ProjectDirectoryKey) as string)!);
        var outputDirectory = Path.GetFullPath(AppContext.BaseDirectory);

        await Assert.That(outputDirectory.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase)).IsFalse();
    }
}
