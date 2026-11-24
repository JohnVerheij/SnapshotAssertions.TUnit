using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using TUnit.Assertions.Core;
using TUnit.Core;

namespace SnapshotAssertions.TUnit;

/// <summary>
/// Asserts that a string is the block of lines between two marker lines of a document, such as a generated table in
/// the project's Markdown files; in accept mode (<c>SNAPSHOT_ACCEPT=1</c>) a block that differs is written there.
/// Created by <c>MatchesDocumentBlock(documentPath, beginMarker, endMarker)</c>; the comparison is
/// <see cref="DocumentBlockEvaluator"/>'s.
/// </summary>
/// <remarks>
/// A misuse or a file problem is not reported as a failed assertion: it propagates as its own exception when the
/// assertion is awaited. That is an <see cref="InvalidOperationException"/> for a path that is not fully qualified when
/// the build recorded no existing project directory, an <see cref="ArgumentException"/> for a line of the string that is
/// a marker line or ends with a carriage return, an <see cref="InvalidDataException"/> for a document that is not valid
/// text in its encoding, and an <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when the document
/// cannot be read or written, or, in accept mode, is a symbolic link (see <see cref="DocumentBlockEvaluator"/>). A
/// document without the block fails the assertion. The evaluation observes the running test's cancellation token.
/// </remarks>
public sealed class DocumentBlockAssertion : Assertion<string>
{
    private readonly string _documentPath;
    private readonly string _beginMarker;
    private readonly string _endMarker;

    /// <summary>Initializes the assertion.</summary>
    /// <param name="context">The assertion context of the string source.</param>
    /// <param name="documentPath">The document, absolute or relative to the test project's directory.</param>
    /// <param name="beginMarker">The line that opens the block.</param>
    /// <param name="endMarker">The line that closes the block.</param>
    internal DocumentBlockAssertion(AssertionContext<string> context, string documentPath, string beginMarker, string endMarker)
        : base(context)
    {
        _documentPath = documentPath;
        _beginMarker = beginMarker;
        _endMarker = endMarker;
    }

    /// <inheritdoc />
    protected override async Task<AssertionResult> CheckAsync(EvaluationMetadata<string> metadata)
    {
        if (metadata.Exception is not null)
            return AssertionResult.Failed($"threw {metadata.Exception.GetType().Name}", metadata.Exception);

        var content = metadata.Value;
        if (content is null)
            return AssertionResult.Failed("actual content was null");

        // An unknown project directory for a relative path, invalid markers, and IO failures propagate to the test
        // runner rather than being downgraded to a failed assertion: they are misuse or environment signals.
        var path = ResolveDocumentPath(_documentPath, SnapshotAssertionImpl.GetProjectDirectory());
        // The test's token, so that a test timeout also ends a wait for another process's lock in accept mode.
        var cancellationToken = TestContext.Current?.Execution.CancellationToken ?? CancellationToken.None;
        var result = await DocumentBlockEvaluator.EvaluateAsync(content, path, _beginMarker, _endMarker, cancellationToken).ConfigureAwait(false);
        return result.IsPass ? AssertionResult.Passed : AssertionResult.Failed(result.Describe());
    }

    /// <inheritdoc />
    protected override string GetExpectation() => "to match the block between the document's markers";

    /// <summary>
    /// Resolves the document path: a fully qualified path as it is, any other against the test project's directory,
    /// which the package's build targets record (on Windows, <c>\docs\x.md</c> takes that directory's drive).
    /// </summary>
    /// <param name="documentPath">The document path as passed.</param>
    /// <param name="projectDirectory">The test project's directory, or <see langword="null"/> when unknown.</param>
    /// <returns>The full path.</returns>
    /// <exception cref="InvalidOperationException">The path is relative and the project directory is unknown.</exception>
    internal static string ResolveDocumentPath(string documentPath, string? projectDirectory)
    {
        if (Path.IsPathFullyQualified(documentPath))
            return Path.GetFullPath(documentPath);

        if (projectDirectory is null)
        {
            throw new InvalidOperationException(
                "MatchesDocumentBlock resolves the relative path '" + MessageText.EscapeLineBreaks(documentPath) + "' against the test " +
                "project's directory, which the package's build targets record as the runtime option " +
                SnapshotAssertionImpl.ProjectDirectoryKey + "; this run has no such directory (the option is missing, or the " +
                "directory does not exist). Pass an absolute path.");
        }

        return Path.GetFullPath(documentPath, projectDirectory);
    }
}
