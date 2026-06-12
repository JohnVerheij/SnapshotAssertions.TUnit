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
        else if (state.TotalDiffering is 0 && !string.Equals(expected, actual, StringComparison.Ordinal))
            EmitLineEndingHint(writer, expected, actual);

        return writer.ToString();
    }

    private static void EmitLine(TextWriter writer, string[] expectedLines, string[] actualLines, int i, ref DiffState state)
    {
        var hasExpected = i < expectedLines.Length;
        var hasActual = i < actualLines.Length;

        if (LinesMatch(hasExpected, hasActual, expectedLines, actualLines, i))
        {
            EmitMatchingLine(writer, expectedLines[i], ref state);
            return;
        }

        AccumulateDifferingTotal(hasExpected, hasActual, ref state);
        if (state.Truncated)
            return;

        EmitDifferingPair(writer, hasExpected, hasActual, expectedLines, actualLines, i, ref state);
    }

    private static bool LinesMatch(bool hasExpected, bool hasActual, string[] expectedLines, string[] actualLines, int i)
        => hasExpected && hasActual && string.Equals(expectedLines[i], actualLines[i], StringComparison.Ordinal);

    private static void EmitMatchingLine(TextWriter writer, string content, ref DiffState state)
    {
        if (!state.Truncated)
            WriteLine(writer, ' ', content);
    }

    private static void AccumulateDifferingTotal(bool hasExpected, bool hasActual, ref DiffState state)
    {
        state.TotalDiffering += hasExpected ? 1 : 0;
        state.TotalDiffering += hasActual ? 1 : 0;
    }

    // Check before each write so a replacement-style diff (one '-' line followed by one
    // '+' line on the same position) cannot exceed MaxDifferingLines by emitting both
    // lines after the cap. The post-write toggle the previous implementation used could
    // emit MaxDifferingLines + 1 lines.
    private static void EmitDifferingPair(TextWriter writer, bool hasExpected, bool hasActual, string[] expectedLines, string[] actualLines, int i, ref DiffState state)
    {
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

    // The diff renderer is intentionally synchronous: callers pass an in-memory StringWriter
    // (the implementation in Render and the standard consumer pattern), there is no IO behind
    // the TextWriter. Meziantou MA0045 fires generically on TextWriter.Write / .WriteLine
    // without inspecting the underlying writer; the async variants would force the method
    // signature to Task-returning for zero IO benefit. Suppress at the methods that emit
    // small literal fragments.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "TextWriter writes are dispatched against an in-memory StringWriter; no IO occurs and making the helper async would propagate Task-returning signatures with no benefit.")]
    private static void EmitTruncationFooter(TextWriter writer, int totalDiffering)
    {
        writer.Write("... (truncated; ");
        writer.Write(totalDiffering.ToString(CultureInfo.InvariantCulture));
        writer.Write(" differing line(s) in total, showing first ");
        writer.Write(MaxDifferingLines.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine(")");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "TextWriter writes are dispatched against an in-memory StringWriter; no IO occurs and making the helper async would propagate Task-returning signatures with no benefit.")]
    private static void WriteLine(TextWriter writer, char prefix, string content)
    {
        writer.Write(prefix);
        writer.WriteLine(content);
    }

    // Emitted when the strings are unequal but the line-by-line view found no differing lines: the
    // only difference is in the line endings themselves, which SplitLines consumes (so the diff above
    // shows every line as context). Names each side's detected ending so the cause is obvious rather
    // than a "did not match" with a diff that shows nothing.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "TextWriter writes are dispatched against an in-memory StringWriter; no IO occurs and making the helper async would propagate Task-returning signatures with no benefit.")]
    private static void EmitLineEndingHint(TextWriter writer, string expected, string actual)
    {
        writer.Write("(no per-line differences: the content differs only in line endings, which the line view normalizes away. expected: ");
        writer.Write(DescribeLineEndings(expected));
        writer.Write(", actual: ");
        writer.Write(DescribeLineEndings(actual));
        writer.WriteLine(". Normalize with SnapshotOptions line-ending handling (for example NormalizeToLF), or enforce a consistent ending with a .gitattributes 'eol=lf' rule.)");
    }

    private static string DescribeLineEndings(string content)
    {
        var crlf = content.Contains("\r\n", StringComparison.Ordinal);
        var lf = false;
        var cr = false;
        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c is '\n' && (i is 0 || content[i - 1] is not '\r'))
                lf = true;
            else if (c is '\r' && (i + 1 >= content.Length || content[i + 1] is not '\n'))
                cr = true;
        }

        // Only called from the hint path, which fires only when the two strings differ by their line
        // endings alone, so at least one ending kind is always present (no zero-ending case to handle).
        var kinds = (crlf ? 1 : 0) + (lf ? 1 : 0) + (cr ? 1 : 0);
        if (kinds > 1)
            return "mixed line endings";
        if (crlf)
            return "CRLF";
        return lf ? "LF" : "CR";
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
