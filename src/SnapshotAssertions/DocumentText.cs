using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SnapshotAssertions;

/// <summary>
/// A document read for a block comparison: its text, the encoding and byte order mark it was read with (so that a
/// rewrite keeps both), and its lines. A line ends at a line feed; a carriage return right before it belongs to the line
/// break, any other carriage return to the line.
/// </summary>
internal sealed class DocumentText
{
    /// <summary>Initializes a document from its decoded text.</summary>
    /// <param name="text">The text, without the byte order mark.</param>
    /// <param name="encoding">The encoding the text was decoded with.</param>
    /// <param name="preamble">The byte order mark the file starts with; empty when it has none.</param>
    private DocumentText(string text, Encoding encoding, byte[] preamble)
    {
        Text = text;
        _encoding = encoding;
        _preamble = preamble;
        Lines = Split(text);
    }

    /// <summary>The text, without the byte order mark.</summary>
    internal string Text { get; }

    /// <summary>The lines, in document order.</summary>
    internal IReadOnlyList<DocumentLine> Lines { get; }

    /// <summary>
    /// Reads a document as UTF-8, or as UTF-16 or UTF-32 when it starts with that byte order mark. A document whose bytes
    /// are not valid text in that encoding is refused, as a rewrite would change bytes outside the block.
    /// </summary>
    /// <param name="path">The full path of the document.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The document.</returns>
    /// <exception cref="InvalidDataException">The document is not valid text in its encoding.</exception>
    internal static async Task<DocumentText> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await BaselineReader.ReadAllBytesSharedAsync(path, cancellationToken).ConfigureAwait(false);
        var (encoding, markLength) = BaselineReader.Detect(bytes);
        var body = bytes.AsSpan(markLength);
        var text = encoding.GetString(body);
        if (!body.SequenceEqual(encoding.GetBytes(text)))
        {
            throw new InvalidDataException(string.Format(
                CultureInfo.InvariantCulture,
                "The document '{0}' is not valid {1} text, so its block cannot be compared or written. A document is read as UTF-8, or as UTF-16 or UTF-32 when it starts with that byte order mark.",
                MessageText.EscapeLineBreaks(path),
                encoding.WebName));
        }

        return new DocumentText(text, encoding, bytes[..markLength]);
    }

    /// <summary>Returns whether a line's content, without its line break, is the given text (ordinally), without
    /// allocating the line.</summary>
    /// <param name="index">The 0-based line index.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> on a match.</returns>
    internal bool LineEquals(int index, string text)
    {
        var line = Lines[index];
        return Text.AsSpan(line.Start, line.Length).SequenceEqual(text);
    }

    /// <summary>Returns the content of a line, without its line break.</summary>
    /// <param name="index">The 0-based line index.</param>
    /// <returns>The line's content.</returns>
    internal string Content(int index)
    {
        var line = Lines[index];
        return Text.Substring(line.Start, line.Length);
    }

    /// <summary>
    /// Returns the document's bytes with the lines between two lines replaced: its byte order mark, then the text up to
    /// and including the line break of line <paramref name="begin"/>, each new line followed by that same line break, and
    /// the text from line <paramref name="end"/> on.
    /// </summary>
    /// <param name="begin">The 0-based index of the begin marker's line; it has a line break, as a line follows it.</param>
    /// <param name="end">The 0-based index of the end marker's line.</param>
    /// <param name="block">The new lines.</param>
    /// <returns>The rewritten document, encoded as it was read.</returns>
    internal byte[] ReplaceBetween(int begin, int end, IReadOnlyList<string> block)
    {
        var beginLine = Lines[begin];
        var blockStart = beginLine.Start + beginLine.Length + beginLine.BreakLength;
        var lineBreak = Text.Substring(beginLine.Start + beginLine.Length, beginLine.BreakLength);
        var builder = new StringBuilder(Text.Length);
        builder.Append(Text, 0, blockStart);
        foreach (var line in block)
            builder.Append(line).Append(lineBreak);
        builder.Append(Text, Lines[end].Start, Text.Length - Lines[end].Start);

        var body = _encoding.GetBytes(builder.ToString());
        return [.. _preamble, .. body];
    }

    /// <summary>
    /// Splits the lines of an actual block: a carriage return and line feed or a line feed ends a line, and a line break
    /// at the end of the text ends the last line rather than starting an empty one. The empty text has no lines.
    /// </summary>
    /// <param name="block">The actual block.</param>
    /// <returns>The block's lines.</returns>
    internal static string[] SplitBlock(string block)
    {
        if (block.Length is 0)
            return [];

        var normalized = block.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (normalized[^1] is '\n')
            normalized = normalized[..^1];
        return normalized.Split('\n');
    }

    /// <summary>Finds the lines of a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Each line's start, content length, and line-break length; text after the last line feed is a line
    /// without a line break when it is not empty.</returns>
    private static List<DocumentLine> Split(string text)
    {
        var lines = new List<DocumentLine>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not '\n')
                continue;

            var withCarriageReturn = i > start && text[i - 1] is '\r';
            var length = i - start - (withCarriageReturn ? 1 : 0);
            lines.Add(new DocumentLine(start, length, withCarriageReturn ? 2 : 1));
            start = i + 1;
        }

        if (start < text.Length)
            lines.Add(new DocumentLine(start, text.Length - start, 0));
        return lines;
    }

    /// <summary>The encoding the text was decoded with.</summary>
    private readonly Encoding _encoding;

    /// <summary>The byte order mark the file starts with; empty when it has none.</summary>
    private readonly byte[] _preamble;
}
