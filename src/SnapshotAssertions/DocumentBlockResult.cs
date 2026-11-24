using System;
using System.Globalization;
using System.IO;

namespace SnapshotAssertions;

/// <summary>
/// The outcome of a document block comparison (<see cref="DocumentBlockEvaluator"/>): the classification, the document,
/// and a description for failure messages.
/// </summary>
public sealed class DocumentBlockResult
{
    /// <summary>Initializes a result.</summary>
    /// <param name="outcome">The classification.</param>
    /// <param name="documentPath">The full path of the document.</param>
    /// <param name="line">For a mismatch, the 1-based document line of the first difference; otherwise 0.</param>
    /// <param name="detail">For a mismatch, the rendered diff; for a block that was not found, what is missing;
    /// otherwise <see langword="null"/>.</param>
    /// <param name="beginMarker">The begin marker, for the description of a block that was not found.</param>
    /// <param name="endMarker">The end marker, for the description of a block that was not found.</param>
    private DocumentBlockResult(
        DocumentBlockOutcome outcome, string documentPath, int line, string? detail, string? beginMarker, string? endMarker)
    {
        Outcome = outcome;
        DocumentPath = documentPath;
        _line = line;
        _detail = detail;
        _beginMarker = beginMarker;
        _endMarker = endMarker;
    }

    /// <summary>The classification of the comparison.</summary>
    public DocumentBlockOutcome Outcome { get; }

    /// <summary>The full path of the document.</summary>
    public string DocumentPath { get; }

    /// <summary>Whether the comparison passes: <see cref="DocumentBlockOutcome.Matched"/> and
    /// <see cref="DocumentBlockOutcome.Accepted"/> pass, the others fail.</summary>
    public bool IsPass => Outcome is DocumentBlockOutcome.Matched or DocumentBlockOutcome.Accepted;

    /// <summary>Renders a multi-line description of the result for failure messages: the document (with the line of the
    /// first difference for a mismatch), the diff or what is missing, and how to accept. A carriage return or line feed
    /// in the path is shown escaped (<c>\r</c>, <c>\n</c>), so the path stays on its line.</summary>
    /// <returns>A multi-line description; the format is not stable.</returns>
    public string Describe()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteDescription(writer);
        return writer.ToString();
    }

    /// <summary>Writes the same description as <see cref="Describe"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">The destination text writer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "The synchronous TextWriter contract of the method; Describe passes an in-memory StringWriter.")]
    public void WriteDescription(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var path = MessageText.EscapeLineBreaks(DocumentPath);
        // An if/else chain rather than a switch: a switch over the enum compiles to a jump table with an out-of-range
        // branch that no DocumentBlockOutcome value can reach.
        if (Outcome is DocumentBlockOutcome.Matched)
        {
            writer.Write("Document block matched: ");
            writer.WriteLine(path);
        }
        else if (Outcome is DocumentBlockOutcome.Accepted)
        {
            writer.Write("Document block accepted (SNAPSHOT_ACCEPT=1): ");
            writer.WriteLine(path);
        }
        else if (Outcome is DocumentBlockOutcome.BlockNotFound)
        {
            WriteNotFound(writer, path);
        }
        else
        {
            WriteMismatch(writer, path);
        }
    }

    /// <summary>Constructs a <see cref="DocumentBlockOutcome.Matched"/> result.</summary>
    /// <param name="documentPath">The full path of the document.</param>
    /// <returns>A passing result.</returns>
    internal static DocumentBlockResult Matched(string documentPath)
        => new(DocumentBlockOutcome.Matched, documentPath, line: 0, detail: null, beginMarker: null, endMarker: null);

    /// <summary>Constructs an <see cref="DocumentBlockOutcome.Accepted"/> result.</summary>
    /// <param name="documentPath">The full path of the document the block was written into.</param>
    /// <returns>A passing result.</returns>
    internal static DocumentBlockResult Accepted(string documentPath)
        => new(DocumentBlockOutcome.Accepted, documentPath, line: 0, detail: null, beginMarker: null, endMarker: null);

    /// <summary>Constructs a <see cref="DocumentBlockOutcome.Mismatched"/> result.</summary>
    /// <param name="documentPath">The full path of the document.</param>
    /// <param name="line">The 1-based document line of the first difference.</param>
    /// <param name="diff">The rendered diff, document lines as <c>-</c>, actual lines as <c>+</c>.</param>
    /// <returns>A failing result.</returns>
    internal static DocumentBlockResult Mismatched(string documentPath, int line, string diff)
        => new(DocumentBlockOutcome.Mismatched, documentPath, line, diff, beginMarker: null, endMarker: null);

    /// <summary>Constructs a <see cref="DocumentBlockOutcome.BlockNotFound"/> result.</summary>
    /// <param name="documentPath">The full path of the document.</param>
    /// <param name="problem">What is missing, as a clause that follows <c>Document block not found: </c>.</param>
    /// <param name="beginMarker">The begin marker.</param>
    /// <param name="endMarker">The end marker.</param>
    /// <returns>A failing result.</returns>
    internal static DocumentBlockResult NotFound(string documentPath, string problem, string beginMarker, string endMarker)
        => new(DocumentBlockOutcome.BlockNotFound, documentPath, line: 0, problem, beginMarker, endMarker);

    /// <summary>Writes the description of a block that was not found.</summary>
    /// <param name="writer">The destination text writer.</param>
    /// <param name="path">The document path, line breaks escaped.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "Helper for the synchronous WriteDescription method.")]
    private void WriteNotFound(TextWriter writer, string path)
    {
        writer.Write("Document block not found: ");
        writer.Write(_detail);
        writer.WriteLine('.');
        writer.Write("  Document:     ");
        writer.WriteLine(path);
        writer.Write("  Begin marker: ");
        writer.WriteLine(_beginMarker);
        writer.Write("  End marker:   ");
        writer.WriteLine(_endMarker);
        writer.WriteLine();
        writer.WriteLine("A block is written only between marker lines the document has: add the begin marker on one line and the");
        writer.WriteLine("end marker on a line after it, then set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to write the block.");
    }

    /// <summary>Writes the description of a mismatch.</summary>
    /// <param name="writer">The destination text writer.</param>
    /// <param name="path">The document path, line breaks escaped.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "Helper for the synchronous WriteDescription method.")]
    private void WriteMismatch(TextWriter writer, string path)
    {
        writer.WriteLine("Document block did not match.");
        writer.Write("  Document: ");
        writer.Write(path);
        writer.Write(':');
        writer.Write(_line.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine(" (the first difference)");
        writer.WriteLine("  Diff:     '-' lines are the document's, '+' lines the actual block's");
        writer.WriteLine();
        writer.Write(_detail);
        writer.WriteLine();
        writer.WriteLine("To write the actual block into the document, set SNAPSHOT_ACCEPT=1 (in a non-CI shell) and run the test again.");
    }

    /// <summary>For a mismatch, the 1-based document line of the first difference; otherwise 0.</summary>
    private readonly int _line;

    /// <summary>For a mismatch, the rendered diff; for a block that was not found, what is missing; otherwise
    /// <see langword="null"/>.</summary>
    private readonly string? _detail;

    /// <summary>The begin marker, for a block that was not found; otherwise <see langword="null"/>.</summary>
    private readonly string? _beginMarker;

    /// <summary>The end marker, for a block that was not found; otherwise <see langword="null"/>.</summary>
    private readonly string? _endMarker;
}
