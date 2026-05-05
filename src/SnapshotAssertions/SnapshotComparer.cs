using System;
using System.Globalization;

namespace SnapshotAssertions;

/// <summary>
/// Pure string-against-string comparison with <see cref="SnapshotOptions"/>-driven
/// normalisation (line endings, BOM, trailing whitespace, trailing newline). Stateless and
/// allocation-friendly; the heavy work of computing a per-line diff is deferred to
/// <see cref="LineDiffRenderer"/> on mismatch.
/// </summary>
public static class SnapshotComparer
{
    private const char Bom = '﻿';

    /// <summary>
    /// Compares <paramref name="actual"/> against <paramref name="expected"/> under
    /// <paramref name="options"/>. Returns <see langword="true"/> if the two are considered
    /// equal under the option-driven normalisation rules; otherwise <see langword="false"/>.
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

        var normalisedActual = Normalise(actual, options);
        var normalisedExpected = Normalise(expected, options);
        return string.Equals(normalisedActual, normalisedExpected, StringComparison.Ordinal);
    }

    /// <summary>
    /// Applies the option-driven normalisation to <paramref name="content"/> and returns the
    /// result. Exposed for diff rendering, which needs to render the same normalised form
    /// the equality check used.
    /// </summary>
    /// <param name="content">The content to normalise.</param>
    /// <param name="options">The options to apply.</param>
    /// <returns>The normalised content.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static string Normalise(string content, SnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        var work = StripBomIfRequested(content, options);

        if (NeedsLineByLineNormalisation(options))
            work = NormaliseLineByLine(work, options);

        return work;
    }

    private static string StripBomIfRequested(string content, SnapshotOptions options)
    {
        if (options.BomHandling == SnapshotBomHandling.StripBom && content.Length > 0 && content[0] == Bom)
            return content[1..];
        return content;
    }

    private static bool NeedsLineByLineNormalisation(SnapshotOptions options)
        => options.LineEndingMode != SnapshotLineEndingMode.Ordinal
            || options.TrailingWhitespace == SnapshotTrailingWhitespace.TrimTrailingPerLine
            || options.TrailingNewline != SnapshotTrailingNewline.Required;

    private static string NormaliseLineByLine(string content, SnapshotOptions options)
    {
        var hasTrailingNewline = content.Length > 0
            && (content[^1] == '\n' || content[^1] == '\r');

        var lines = content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        // If the content ended with a newline, Split adds an empty trailing element. Drop it
        // for trimming/joining; we re-emit a trailing line below per TrailingNewline policy.
        var lastIndex = lines.Length;
        if (hasTrailingNewline && lastIndex > 0 && lines[lastIndex - 1].Length == 0)
            lastIndex--;

        if (options.TrailingWhitespace == SnapshotTrailingWhitespace.TrimTrailingPerLine)
        {
            for (var i = 0; i < lastIndex; i++)
                lines[i] = lines[i].TrimEnd();
        }

        var separator = ResolveSeparator(options.LineEndingMode);
        var joined = string.Join(separator, lines, 0, lastIndex);
        return ApplyTrailingNewlinePolicy(joined, options.TrailingNewline, separator, lastIndex, hasTrailingNewline);
    }

    private static string ResolveSeparator(SnapshotLineEndingMode mode) => mode switch
    {
        SnapshotLineEndingMode.NormalizeToLF => "\n",
        SnapshotLineEndingMode.NormalizeToCRLF => "\r\n",
        SnapshotLineEndingMode.IgnoreLineEndings => string.Empty,
        _ => Environment.NewLine,
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
                // newline but expected has one (or vice versa), the normalised forms differ
                // and the comparison correctly reports a mismatch.
                if (inputHadTrailingNewline && separator.Length > 0 && lineCount > 0)
                    return joined + separator;
                return joined;
            case SnapshotTrailingNewline.Optional:
            case SnapshotTrailingNewline.Forbidden:
                // Both sides normalised to no trailing newline; presence vs absence is
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
