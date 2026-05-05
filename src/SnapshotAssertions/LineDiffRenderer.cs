using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace SnapshotAssertions;

/// <summary>
/// Renders a simple per-line comparison between two strings for use in assertion failure
/// messages. Output uses unified-diff-style line prefixes (<c>-</c> for expected,
/// <c>+</c> for actual) and is truncated to the first 20 differing lines so very large
/// snapshots do not produce an overwhelming wall of text.
/// </summary>
/// <remarks>
/// The renderer is intentionally naive: lines are compared in lockstep by position, with no
/// attempt at longest-common-subsequence alignment. For typical snapshot use (PublicApiGenerator
/// output, rendered audit logs) this produces a useful diff. Consumers needing a
/// well-aligned, insertion-aware diff should rely on the <c>.expected.txt</c> /
/// <c>.actual.txt</c> file paths in the failure message and run their preferred external diff
/// tool against the two files.
/// </remarks>
public static class LineDiffRenderer
{
    /// <summary>The maximum number of differing lines emitted before truncation. Matching
    /// (context) lines are not counted toward this limit.</summary>
    public const int MaxDifferingLines = 20;

    /// <summary>
    /// Renders a line-by-line diff between <paramref name="expected"/> and <paramref name="actual"/>.
    /// </summary>
    /// <param name="expected">The expected baseline content.</param>
    /// <param name="actual">The actual content produced by the test.</param>
    /// <returns>A multi-line diff string, with each line prefixed by <c> </c> (context),
    /// <c>-</c> (expected only), or <c>+</c> (actual only). Truncated when the number of
    /// differing lines exceeds <see cref="MaxDifferingLines"/>.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static string Render(string expected, string actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var expectedLines = SplitLines(expected);
        var actualLines = SplitLines(actual);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var state = new DiffState();

        var max = Math.Max(expectedLines.Length, actualLines.Length);
        for (var i = 0; i < max; i++)
            EmitLine(writer, expectedLines, actualLines, i, ref state);

        if (state.Truncated)
            EmitTruncationFooter(writer, state.TotalDiffering);

        return writer.ToString();
    }

    private static void EmitLine(TextWriter writer, string[] expectedLines, string[] actualLines, int i, ref DiffState state)
    {
        var hasExpected = i < expectedLines.Length;
        var hasActual = i < actualLines.Length;

        if (hasExpected && hasActual && string.Equals(expectedLines[i], actualLines[i], StringComparison.Ordinal))
        {
            if (!state.Truncated)
                WriteLine(writer, ' ', expectedLines[i]);
            return;
        }

        state.TotalDiffering += hasExpected ? 1 : 0;
        state.TotalDiffering += hasActual ? 1 : 0;

        if (state.Truncated)
            return;

        // Check before each write so a replacement-style diff (one '-' line followed by one
        // '+' line on the same position) cannot exceed MaxDifferingLines by emitting both
        // lines after the cap. The post-write toggle the previous implementation used could
        // emit MaxDifferingLines + 1 lines.
        if (hasExpected && !TryEmitDifferingLine(writer, '-', expectedLines[i], ref state))
            return;
        if (hasActual)
            TryEmitDifferingLine(writer, '+', actualLines[i], ref state);
    }

    private static bool TryEmitDifferingLine(TextWriter writer, char prefix, string content, ref DiffState state)
    {
        if (state.DifferingEmitted >= MaxDifferingLines)
        {
            state.Truncated = true;
            return false;
        }
        WriteLine(writer, prefix, content);
        state.DifferingEmitted++;
        return true;
    }

    private static void EmitTruncationFooter(TextWriter writer, int totalDiffering)
    {
        writer.Write("... (truncated; ");
        writer.Write(totalDiffering.ToString(CultureInfo.InvariantCulture));
        writer.Write(" differing line(s) in total, showing first ");
        writer.Write(MaxDifferingLines.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine(")");
    }

    private static void WriteLine(TextWriter writer, char prefix, string content)
    {
        writer.Write(prefix);
        writer.WriteLine(content);
    }

    private static string[] SplitLines(string content)
        => content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

    [StructLayout(LayoutKind.Auto)]
    private struct DiffState
    {
        public int DifferingEmitted;
        public int TotalDiffering;
        public bool Truncated;
    }
}
