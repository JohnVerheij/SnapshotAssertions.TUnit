using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;
using TUnit.Assertions.Exceptions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the complete failure text the adapter produces, line by line: a new baseline, a mismatch with its diff and
/// the project baseline line, a difference in line endings only, and a trailing-newline policy violation. The format
/// is documented as not stable; these tests exist so that a change to it is a decision, and so that the paths and
/// the accept guidance are checked in their final position, not only by <c>Contains</c>.
/// </summary>
[Timeout(15_000)]
internal sealed class FailureMessageTests
{
    /// <summary>A missing baseline: both paths, then the guidance to accept it.</summary>
    [Test]
    public async Task NoBaseline_MessageIsPinned(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByFile(Path.Combine(directory, "m.expected.txt"));

            var result = await SnapshotAssertionImpl.EvaluateAsync("a\n", null, SnapshotOptions.Default, paths, null, cancellationToken);

            await Assert.That(Lf(result.Message)).IsEqualTo(
                "Snapshot baseline does not exist.\n"
                + $"  Expected: {paths.ExpectedFilePath}\n"
                + $"  Actual:   {paths.ActualFilePath}\n"
                + "\n"
                + "To accept it as the baseline, inspect the actual file and copy it to the expected path\n"
                + "(for an .expected.txt file in the build output's Snapshots/ folder, to the matching path in the project's Snapshots/ folder instead),\n"
                + "or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.\n");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A mismatch: both paths, the line diff, the accept guidance and, last, the project baseline line.</summary>
    [Test]
    public async Task Mismatch_MessageWithProjectBaselineIsPinned(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByFile(Path.Combine(directory, "m.expected.txt"));
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "a\nb\n", cancellationToken);
            var projectBaseline = Path.Combine(directory, "project", "m.expected.txt");

            var result = await SnapshotAssertionImpl.EvaluateAsync("a\nc\n", null, SnapshotOptions.Default, paths, projectBaseline, cancellationToken);

            await Assert.That(Lf(result.Message)).IsEqualTo(
                "Snapshot did not match the baseline.\n"
                + $"  Expected: {paths.ExpectedFilePath}\n"
                + $"  Actual:   {paths.ActualFilePath}\n"
                + "\n"
                + " a\n"
                + "-b\n"
                + "+c\n"
                + " \n"
                + "\n"
                + "To accept the change, copy the actual file over the expected file\n"
                + "(for an .expected.txt file in the build output's Snapshots/ folder, over the one at the matching path in the project's Snapshots/ folder),\n"
                + "or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.\n"
                + $"Project baseline: {projectBaseline}\n");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Content that differs only in its line endings: the diff shows every line as context and the hint names both endings.</summary>
    [Test]
    public async Task LineEndingOnlyDifference_MessageIsPinned(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByFile(Path.Combine(directory, "m.expected.txt"));
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "a\nb\n", cancellationToken);

            var result = await SnapshotAssertionImpl.EvaluateAsync("a\r\nb\r\n", null, SnapshotOptions.Default, paths, null, cancellationToken);

            await Assert.That(Lf(result.Message)).IsEqualTo(
                "Snapshot did not match the baseline.\n"
                + $"  Expected: {paths.ExpectedFilePath}\n"
                + $"  Actual:   {paths.ActualFilePath}\n"
                + "\n"
                + " a\n"
                + " b\n"
                + " \n"
                + "(no per-line differences: the content differs only in line endings, which the line view normalizes away. expected: LF, actual: CRLF. Normalize with SnapshotOptions line-ending handling (for example NormalizeToLF), or enforce a consistent ending with a .gitattributes 'eol=lf' rule.)\n"
                + "\n"
                + "To accept the change, copy the actual file over the expected file\n"
                + "(for an .expected.txt file in the build output's Snapshots/ folder, over the one at the matching path in the project's Snapshots/ folder),\n"
                + "or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.\n");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A trailing newline under <c>Forbidden</c>: the note names both sides and the ways out.</summary>
    [Test]
    public async Task ForbiddenTrailingNewline_MessageIsPinned(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByFile(Path.Combine(directory, "m.expected.txt"));
            await File.WriteAllTextAsync(paths.ExpectedFilePath, "a\nb\n", cancellationToken);
            var options = SnapshotOptions.Default with { TrailingNewline = SnapshotTrailingNewline.Forbidden };

            var result = await SnapshotAssertionImpl.EvaluateAsync("a\nb\n", null, options, paths, null, cancellationToken);

            await Assert.That(Lf(result.Message)).IsEqualTo(
                "Snapshot did not match the baseline.\n"
                + $"  Expected: {paths.ExpectedFilePath}\n"
                + $"  Actual:   {paths.ActualFilePath}\n"
                + "\n"
                + " a\n"
                + " b\n"
                + " \n"
                + "(trailing newline policy: SnapshotTrailingNewline.Forbidden does not allow a trailing newline on either side, but both the expected baseline and the actual content end with a trailing newline. Remove it from the producer and the baseline, or choose SnapshotTrailingNewline.Optional to ignore it or Required to preserve and compare it.)\n"
                + "\n"
                + "To accept the change, copy the actual file over the expected file\n"
                + "(for an .expected.txt file in the build output's Snapshots/ folder, over the one at the matching path in the project's Snapshots/ folder),\n"
                + "or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.\n");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Through the public chain, a <c>.AtPath(...)</c> failure carries the core text and no project baseline line: the
    /// caller chose the file, so there is no project-side counterpart to name.
    /// </summary>
    [Test]
    public async Task AtPathFailure_HasNoProjectBaselineLine(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var expected = Path.Combine(directory, "chain.expected.txt");
            await File.WriteAllTextAsync(expected, "old\n", cancellationToken);

            var exception = await Assert.That(async () => await Assert.That("new\n").MatchesSnapshot().AtPath(expected))
                .Throws<AssertionException>();

            await Assert.That(exception!.Message).Contains("Snapshot did not match the baseline.");
            await Assert.That(exception.Message).Contains(expected);
            await Assert.That(exception.Message).DoesNotContain("Project baseline:");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A project baseline path with a line break (valid in a file name on Linux) is shown escaped on its one line.
    /// </summary>
    [Test]
    public async Task ProjectBaselinePathWithLineBreak_IsEscapedOnItsLine(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var paths = SnapshotFileResolver.ResolveByFile(Path.Combine(directory, "m.expected.txt"));

            var result = await SnapshotAssertionImpl.EvaluateAsync("a\n", null, SnapshotOptions.Default, paths, "project/p\r\nq.expected.txt", cancellationToken);

            await Assert.That(Lf(result.Message)).EndsWith("\nProject baseline: project/p\\r\\nq.expected.txt\n");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A renderer that throws a multi-line message ("Cannot access a disposed object.\r\nObject name: 'x'." is typical)
    /// is reported on the one <c>but renderer threw</c> clause, with the line breaks escaped.
    /// </summary>
    [Test]
    public async Task RendererThrowingMultiLineMessage_IsReportedOnOneClause(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var subject = 1;

        var exception = await Assert.That(async () => await Assert.That(subject)
                .MatchesSnapshot(static _ => throw new InvalidOperationException("first line\r\nsecond line\nthird line"))
                .AtPath("unused.expected.txt"))
            .Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("renderer threw InvalidOperationException: first line\\r\\nsecond line\\nthird line");
        await Assert.That(Lf(exception.Message)).DoesNotContain("first line\nsecond line");
    }

    /// <summary>
    /// A snapshot name or path with a line break is escaped in the assertion expression TUnit prints, so the expression
    /// stays on one line.
    /// </summary>
    [Test]
    public async Task NameAndPathWithLineBreak_AreEscapedInTheExpression(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? nothing = null;
        int? none = null;

        var named = await Assert.That(async () => await Assert.That(nothing).MatchesSnapshot().WithName("n\r\nm").AtPath("p\nq"))
            .Throws<AssertionException>();
        var renderedNamed = await Assert.That(async () => await Assert.That(none).MatchesSnapshot(static _ => "rendered").WithName("n\r\nm").AtPath("p\nq"))
            .Throws<AssertionException>();

        foreach (var message in new[] { named!.Message, renderedNamed!.Message })
        {
            await Assert.That(message).Contains(".WithName(\"n\\r\\nm\").AtPath(\"p\\nq\")");
            await Assert.That(Lf(message)).DoesNotContain("n\nm");
            await Assert.That(Lf(message)).DoesNotContain("p\nq");
        }
    }

    private static string Lf(string text) => text.ReplaceLineEndings("\n");

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "failure-message-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
