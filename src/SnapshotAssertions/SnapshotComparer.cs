using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SnapshotAssertions;

/// <summary>
/// Pure string-against-string comparison with <see cref="SnapshotOptions"/>-driven
/// normalization (line endings, BOM, trailing whitespace, trailing newline). Stateless and
/// allocation-friendly; the heavy work of computing a per-line diff is deferred to
/// <see cref="LineDiffRenderer"/> on mismatch.
/// </summary>
public static class SnapshotComparer
{
    private const char Bom = '﻿';

    /// <summary>
    /// Compares <paramref name="actual"/> against <paramref name="expected"/> under
    /// <paramref name="options"/>. Returns <see langword="true"/> if the two are considered
    /// equal under the option-driven normalization rules; otherwise <see langword="false"/>.
    /// </summary>
    /// <param name="actual">The actual content produced by the test.</param>
    /// <param name="expected">The expected baseline content.</param>
    /// <param name="options">The comparison options. Required (use <see cref="SnapshotOptions.Default"/>
    /// for strict-default semantics).</param>
    /// <returns><see langword="true"/> if the two strings are considered equal.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static bool AreEqual(string actual, string expected, SnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(options);

        var normalizedActual = Normalize(actual, options);
        var normalizedExpected = Normalize(expected, options);
        return string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal);
    }

    /// <summary>
    /// Applies the option-driven normalization to <paramref name="content"/> and returns the
    /// result. Exposed for diff rendering, which needs to render the same normalized form
    /// the equality check used.
    /// </summary>
    /// <param name="content">The content to normalize.</param>
    /// <param name="options">The options to apply.</param>
    /// <returns>The normalized content.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static string Normalize(string content, SnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        var work = content;
        if (options.Normalizer is not null)
        {
            work = options.Normalizer(work)
                ?? throw new InvalidOperationException(
                    "The configured SnapshotOptions.Normalizer returned null; a normalizer must return a string.");
        }

        work = StripBomIfRequested(work, options);

        if (NeedsLineByLineNormalization(options))
            work = NormalizeLineByLine(work, options);

        return work;
    }

    private static string StripBomIfRequested(string content, SnapshotOptions options)
    {
        if (options.BomHandling is SnapshotBomHandling.StripBom && content.Length > 0 && content[0] == Bom)
            return content[1..];
        return content;
    }

    private static bool NeedsLineByLineNormalization(SnapshotOptions options)
        => options.LineEndingMode is not SnapshotLineEndingMode.Ordinal
            || options.TrailingWhitespace is SnapshotTrailingWhitespace.TrimTrailingPerLine
            || options.TrailingNewline is not SnapshotTrailingNewline.Required;

    private static string NormalizeLineByLine(string content, SnapshotOptions options)
    {
        var (lines, terminators) = SplitKeepingTerminators(content);

        var hasTrailingNewline = content.Length > 0
            && (content[^1] == '\n' || content[^1] == '\r');

        // When the content ended with a newline, the split adds an empty trailing element. Drop it
        // for trimming/joining; the trailing newline itself is re-emitted below per the policy.
        var lastIndex = lines.Length;
        if (hasTrailingNewline && lastIndex > 0 && lines[lastIndex - 1].Length is 0)
            lastIndex--;

        if (options.TrailingWhitespace is SnapshotTrailingWhitespace.TrimTrailingPerLine)
        {
            for (var i = 0; i < lastIndex; i++)
                lines[i] = lines[i].TrimEnd();
        }

        // Ordinal mode preserves each line's original terminator (so mixed CRLF / LF endings, and the
        // platform on which accept-mode runs, do not change the canonical bytes). The normalizing
        // modes rejoin with one uniform separator.
        return options.LineEndingMode is SnapshotLineEndingMode.Ordinal
            ? JoinPreservingTerminators(lines, terminators, lastIndex, options.TrailingNewline, hasTrailingNewline)
            : JoinWithUniformSeparator(lines, lastIndex, options, hasTrailingNewline);
    }

    private static string JoinWithUniformSeparator(string[] lines, int lastIndex, SnapshotOptions options, bool hasTrailingNewline)
    {
        var separator = ResolveSeparator(options.LineEndingMode);
        var joined = string.Join(separator, lines, 0, lastIndex);
        return ApplyTrailingNewlinePolicy(joined, options.TrailingNewline, separator, lastIndex, hasTrailingNewline);
    }

    private static string JoinPreservingTerminators(
        string[] lines, string[] terminators, int lastIndex, SnapshotTrailingNewline policy, bool hasTrailingNewline)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < lastIndex; i++)
            sb.Append(lines[i]).Append(terminators[i]);

        // Required preserves the original terminators as written. Optional and Forbidden strip a
        // trailing terminator so its presence versus absence is unobservable to the comparison.
        if (policy is not SnapshotTrailingNewline.Required && hasTrailingNewline && lastIndex > 0)
        {
            var lastTerminator = terminators[lastIndex - 1];
            sb.Length -= lastTerminator.Length;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Splits <paramref name="content"/> into line texts and the terminator that followed each line
    /// (<c>"\r\n"</c>, <c>"\n"</c>, <c>"\r"</c>, or <see cref="string.Empty"/> for the final segment).
    /// The line texts match <c>string.Split(["\r\n", "\n", "\r"])</c>; the parallel terminator array
    /// lets Ordinal mode rejoin without losing the original endings.
    /// </summary>
    private static (string[] Lines, string[] Terminators) SplitKeepingTerminators(string content)
    {
        var lines = new List<string>();
        var terminators = new List<string>();

        var start = 0;
        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];
            if (c == '\n')
            {
                lines.Add(content[start..i]);
                terminators.Add("\n");
                i++;
                start = i;
            }
            else if (c == '\r')
            {
                var crlf = i + 1 < content.Length && content[i + 1] == '\n';
                lines.Add(content[start..i]);
                terminators.Add(crlf ? "\r\n" : "\r");
                i += crlf ? 2 : 1;
                start = i;
            }
            else
            {
                i++;
            }
        }

        lines.Add(content[start..]);
        terminators.Add(string.Empty);
        return (lines.ToArray(), terminators.ToArray());
    }

    private static string ResolveSeparator(SnapshotLineEndingMode mode) => mode switch
    {
        SnapshotLineEndingMode.NormalizeToCRLF => "\r\n",
        SnapshotLineEndingMode.IgnoreLineEndings => string.Empty,
        _ => "\n", // NormalizeToLF; Ordinal never reaches here (handled by JoinPreservingTerminators).
    };

    private static string ApplyTrailingNewlinePolicy(
        string joined,
        SnapshotTrailingNewline policy,
        string separator,
        int lineCount,
        bool inputHadTrailingNewline)
    {
        switch (policy)
        {
            case SnapshotTrailingNewline.Required:
                // Preserve the trailing-newline state of the input. If actual lacks a trailing
                // newline but expected has one (or vice versa), the normalized forms differ
                // and the comparison correctly reports a mismatch.
                //
                // Special case: under SnapshotLineEndingMode.IgnoreLineEndings the separator
                // is empty, so the natural append-separator path produces no trailing marker.
                // To preserve the Required semantics in that mode, append a stable LF marker
                // when the input had a trailing newline. Both sides go through this same path,
                // so an LF appended on both sides cancels out on equal inputs and creates a
                // visible difference between "foo" and "foo\n".
                if (inputHadTrailingNewline)
                {
                    if (separator.Length > 0 && lineCount > 0)
                        return joined + separator;
                    if (separator.Length is 0)
                        return joined + "\n";
                }
                return joined;
            case SnapshotTrailingNewline.Optional:
            case SnapshotTrailingNewline.Forbidden:
                // Both sides normalized to no trailing newline; presence vs absence is
                // unobservable to the comparison.
                return joined;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(policy),
                    policy,
                    string.Format(CultureInfo.InvariantCulture, "Unknown TrailingNewline value: {0}", policy));
        }
    }
}
