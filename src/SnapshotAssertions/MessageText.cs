using System;

namespace SnapshotAssertions;

/// <summary>
/// Text helpers for failure and exception messages. A message that names a file path in a line of its own
/// (<c>  Expected: ...</c>) or inside one sentence must keep that structure whatever the path holds.
/// </summary>
internal static class MessageText
{
    /// <summary>
    /// Replaces each carriage return and line feed in <paramref name="text"/> with its escaped two-character form
    /// (<c>\r</c>, <c>\n</c>), so a caller-supplied string, such as a path on a file system that allows line breaks in a
    /// file name, cannot split a one-line clause of a message into several lines.
    /// </summary>
    /// <param name="text">The text to render into a message.</param>
    /// <returns>The text with its line breaks escaped; the same text when it holds none.</returns>
    internal static string EscapeLineBreaks(string text)
        => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
