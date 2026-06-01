using System;
using System.Globalization;

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
        var hasTrailingNewline = content.Length > 0
            && (content[^1] == '\n' || content[^1] == '\r');

        var lines = content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        // If the content ended with a newline, Split adds an empty trailing element. Drop it
        // for trimming/joining; we re-emit a trailing line below per TrailingNewline policy.
        var lastIndex = lines.Length;
        if (hasTrailingNewline && lastIndex > 0 && lines[lastIndex - 1].Length is 0)
            lastIndex--;

        if (options.TrailingWhitespace is SnapshotTrailingWhitespace.TrimTrailingPerLine)
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
