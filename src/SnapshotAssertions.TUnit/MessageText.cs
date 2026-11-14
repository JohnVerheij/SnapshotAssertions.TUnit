using System;

namespace SnapshotAssertions.TUnit;

/// <summary>
/// Text helpers for failure messages and the assertion expression. TUnit renders both as line-structured text (a
/// <c>but ...</c> clause, a one-line expression), so a string the caller or a renderer supplies must not add lines.
/// </summary>
internal static class MessageText
{
    /// <summary>
    /// Replaces each carriage return and line feed in <paramref name="text"/> with its escaped two-character form
    /// (<c>\r</c>, <c>\n</c>). A renderer's exception message is often several lines
    /// (<c>"Cannot access a disposed object.\r\nObject name: 'x'."</c>), and a snapshot name or path can hold a line
    /// break on a file system that allows one in a file name.
    /// </summary>
    /// <param name="text">The text to render into a message.</param>
    /// <returns>The text with its line breaks escaped; the same text when it holds none.</returns>
    internal static string EscapeLineBreaks(string text)
        => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
