using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions.TUnit;
using TUnit.Assertions.Exceptions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the <c>MatchesDocumentBlock</c> entry point: a matching block passes, a differing one fails with the evaluator's
/// description, the value and exception paths of the source, the argument checks at the call, and the resolution of a
/// relative document path against the test project's directory.
/// </summary>
[Timeout(15_000)]
internal sealed class MatchesDocumentBlockTests
{
    private const string Begin = "<!-- generated: begin -->";
    private const string End = "<!-- generated: end -->";

    private static readonly ConcurrentBag<string> TempDirectories = [];

    /// <summary>A block that matches the lines between the markers passes.</summary>
    [Test]
    public async Task MatchingBlock_Passes(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"# Manual\n{Begin}\n| a | b |\n{End}\n", cancellationToken);

        await Assert.That("| a | b |\n").MatchesDocumentBlock(path, Begin, End);
    }

    /// <summary>A block that differs fails with the evaluator's description and the assertion expression, and the
    /// document is not changed.</summary>
    [Test]
    public async Task DifferingBlock_FailsWithDescription(CancellationToken cancellationToken)
    {
        var content = $"{Begin}\nold\n{End}\n";
        var path = await CreateDocumentAsync(content, cancellationToken);
        var block = "new\n";

        var exception = await Assert.That(async () => await Assert.That(block).MatchesDocumentBlock(path, Begin, End))
            .Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains($".MatchesDocumentBlock(\"{path}\", ...)");
        await Assert.That(exception.Message).Contains("to match the block between the document's markers");
        await Assert.That(exception.Message).Contains("Document block did not match.");
        await Assert.That(exception.Message).Contains($"{path}:2 (the first difference)");
        await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo(content);
    }

    /// <summary>A document without the block fails with what is missing.</summary>
    [Test]
    public async Task MissingBlock_Fails(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync("no markers\n", cancellationToken);

        var exception = await Assert.That(async () => await Assert.That("a\n").MatchesDocumentBlock(path, Begin, End))
            .Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("Document block not found: no line of the document is the begin marker.");
    }

    /// <summary>A null value fails without reading the document.</summary>
    [Test]
    public async Task NullValue_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? value = null;

        var exception = await Assert.That(async () => await Assert.That(value!).MatchesDocumentBlock("unused.md", Begin, End))
            .Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("actual content was null");
    }

    /// <summary>A source that throws fails with the exception's type, without reading the document.</summary>
    [Test]
    public async Task ThrowingSource_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // An async lazy source routes the exception through TUnit's pipeline into EvaluationMetadata.Exception; a bare
        // Func<string> would resolve to the IEnumerable<char> overload.
        Func<Task<string?>> source = () => Task.FromException<string?>(new InvalidOperationException("no block"));

        var exception = await Assert.That(async () => await Assert.That(source).MatchesDocumentBlock("unused.md", Begin, End))
            .Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("threw InvalidOperationException");
    }

    /// <summary>Arguments are checked at the call, naming the parameter.</summary>
    [Test]
    [Arguments(null, Begin, End, "documentPath")]
    [Arguments(" ", Begin, End, "documentPath")]
    [Arguments("doc.md", null, End, "beginMarker")]
    [Arguments("doc.md", "", End, "beginMarker")]
    [Arguments("doc.md", Begin, null, "endMarker")]
    [Arguments("doc.md", Begin, "a\r\nb", "endMarker")]
    [Arguments("doc.md", "a\nb", End, "beginMarker")]
    [Arguments("doc.md", End, End, "endMarker")]
    public async Task InvalidArgument_ThrowsAtTheCall(string? documentPath, string? beginMarker, string? endMarker, string parameterName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exception = await Assert.That(async () => await Assert.That("a\n").MatchesDocumentBlock(documentPath!, beginMarker!, endMarker!))
            .Throws<ArgumentException>();

        await Assert.That(exception!.ParamName).IsEqualTo(parameterName);
    }

    /// <summary>A null source is refused.</summary>
    [Test]
    public async Task NullSource_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exception = await Assert.That(() => global::TUnit.Assertions.Extensions.MatchesDocumentBlockExtensions.MatchesDocumentBlock(null!, "doc.md", Begin, End))
            .Throws<ArgumentNullException>();

        await Assert.That(exception!.ParamName).IsEqualTo("source");
    }

    /// <summary>This test project references the adapter as a project, so its build does not record the project
    /// directory: a relative path fails with what to do instead, naming the path.</summary>
    [Test]
    public async Task RelativePath_UnknownProjectDirectory_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Skip.When(SnapshotAssertionImpl.GetProjectDirectory() is not null, "The run records a project directory.");

        var exception = await Assert.That(async () => await Assert.That("a\n").MatchesDocumentBlock("docs/manual.md", Begin, End))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("relative path 'docs/manual.md'");
        await Assert.That(exception.Message).Contains("Pass an absolute path.");
    }

    /// <summary>A relative path resolves against the project directory; a rooted path is used as it is.</summary>
    [Test]
    public async Task ResolveDocumentPath_RelativeAndRooted(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project = Path.Combine(Path.GetTempPath(), "project", "tests");
        var rooted = Path.Combine(Path.GetTempPath(), "elsewhere", "doc.md");

        var relative = DocumentBlockAssertion.ResolveDocumentPath(Path.Combine("..", "docs", "manual.md"), project);
        var asIs = DocumentBlockAssertion.ResolveDocumentPath(rooted, projectDirectory: null);

        await Assert.That(relative).IsEqualTo(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "docs", "manual.md")));
        await Assert.That(asIs).IsEqualTo(Path.GetFullPath(rooted));
        if (OperatingSystem.IsWindows())
        {
            // Rooted but not fully qualified: the project directory's drive, not the current directory's.
            // A drive that is not the working directory's, so that resolving against the working directory shows.
            var driveRooted = DocumentBlockAssertion.ResolveDocumentPath(@"\docs\x.md", @"Y:\project\tests");
            await Assert.That(driveRooted).IsEqualTo(@"Y:\docs\x.md");
        }
    }

    /// <summary>Outside a running test (no test context), the assertion runs without a cancellation token.</summary>
    [Test]
    public async Task OutsideATest_RunsWithoutAToken(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\n| a |\n{End}\n", cancellationToken);
        Task run;
        using (ExecutionContext.SuppressFlow())
        {
            run = Task.Run(async () => await Assert.That("| a |\n").MatchesDocumentBlock(path, Begin, End), cancellationToken);
        }

        await run;
    }

    [After(Class)]
    public static void DeleteTempDirectories(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var directory in TempDirectories)
            Directory.Delete(directory, recursive: true);
    }

    private static async Task<string> CreateDocumentAsync(string content, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "matches-document-block-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        TempDirectories.Add(directory);
        var path = Path.Combine(directory, "manual.md");
        await File.WriteAllTextAsync(path, content, cancellationToken);
        return path;
    }
}
