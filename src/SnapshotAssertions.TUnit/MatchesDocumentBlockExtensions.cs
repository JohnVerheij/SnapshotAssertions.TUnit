using System;
using System.Globalization;
using SnapshotAssertions.TUnit;
using TUnit.Assertions.Core;

namespace TUnit.Assertions.Extensions;

/// <summary>
/// The document block entry point: compare a generated block, such as a table built from a register or a list built
/// from code, with the lines between two marker lines of a document, and write it there in accept mode.
/// </summary>
public static class MatchesDocumentBlockExtensions
{
    /// <summary>
    /// Asserts that the string is the block of lines between <paramref name="beginMarker"/> and
    /// <paramref name="endMarker"/> in the document at <paramref name="documentPath"/>. With <c>SNAPSHOT_ACCEPT=1</c> (in
    /// a non-CI environment, or with <c>SNAPSHOT_ACCEPT_IN_CI=1</c>) a block that differs is written between the markers
    /// and the assertion passes; the rest of the document is not changed.
    /// </summary>
    /// <remarks>
    /// The string's lines are each ended by a line feed or a carriage return and line feed (the last line break is
    /// optional), and the empty string is a block of no lines. A marker is a whole line of the document, compared
    /// ordinally; the begin marker must be on exactly one line, and the block ends at the first end marker line after it.
    /// A document without the block fails the assertion, also in accept mode: a block is written only between marker
    /// lines the document has. See <see cref="SnapshotAssertions.DocumentBlockEvaluator"/> for the comparison and the
    /// rewrite.
    /// </remarks>
    /// <param name="source">The string source.</param>
    /// <param name="documentPath">The document: a fully qualified path, or a path relative to the test project's
    /// directory (such as <c>"../../docs/manual.md"</c>).</param>
    /// <param name="beginMarker">The line that opens the block: not empty, without a line break.</param>
    /// <param name="endMarker">The line that closes the block: not empty, without a line break, other than
    /// <paramref name="beginMarker"/>.</param>
    /// <returns>The assertion, to await.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> is empty or white space; a marker is empty or
    /// holds a line break; or the markers are equal.</exception>
    public static DocumentBlockAssertion MatchesDocumentBlock(
        this IAssertionSource<string> source,
        string documentPath,
        string beginMarker,
        string endMarker)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrEmpty(beginMarker);
        ArgumentException.ThrowIfNullOrEmpty(endMarker);
        ThrowIfLineBreak(beginMarker, nameof(beginMarker));
        ThrowIfLineBreak(endMarker, nameof(endMarker));
        if (string.Equals(beginMarker, endMarker, StringComparison.Ordinal))
            throw new ArgumentException("The end marker must differ from the begin marker.", nameof(endMarker));

        source.Context.ExpressionBuilder.Append(
            CultureInfo.InvariantCulture,
            $".MatchesDocumentBlock(\"{MessageText.EscapeLineBreaks(documentPath)}\", ...)");
        return new DocumentBlockAssertion(source.Context, documentPath, beginMarker, endMarker);
    }

    /// <summary>Throws when a marker holds a line break, at the call rather than when the assertion runs.</summary>
    /// <param name="marker">The marker.</param>
    /// <param name="parameterName">The parameter it was passed as.</param>
    /// <exception cref="ArgumentException"><paramref name="marker"/> holds a line break.</exception>
    private static void ThrowIfLineBreak(string marker, string parameterName)
    {
        if (marker.AsSpan().IndexOfAny('\r', '\n') >= 0)
            throw new ArgumentException("A marker is one line: it must not hold a carriage return or line feed.", parameterName);
    }
}
