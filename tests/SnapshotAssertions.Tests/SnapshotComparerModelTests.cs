using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Decision-table tests of the comparison and of the accept round trip, checked against a model of the documented
/// semantics that is written independently of <see cref="SnapshotComparer"/>: every combination of line-ending mode,
/// trailing-whitespace policy, trailing-newline policy and BOM handling is run over a set of contents whose line
/// endings, blank lines, trailing whitespace and byte-order mark differ.
/// </summary>
[Category("Smoke")]
[Timeout(60_000)]
internal sealed class SnapshotComparerModelTests
{
    private static readonly string Bom = char.ConvertFromUtf32(0xFEFF);

    private static readonly string[] Samples =
    [
        string.Empty, "a", "a\n", "a\r\n", "a\r", "a\nb", "a\r\nb", "a\nb\n", "a\r\nb\r\n", "a\rb\r", "a\n\n", "a\r\n\r\n",
        "a \nb\t\n", "a \r\nb\t\r\n", "a\r\nb\n", "\n", "\r\n", " \n", "a\n \n", Bom + "a\n",
    ];

    private static readonly SnapshotLineEndingMode[] LineEndingModes =
    [
        SnapshotLineEndingMode.Ordinal,
        SnapshotLineEndingMode.NormalizeToLF,
        SnapshotLineEndingMode.NormalizeToCRLF,
    ];

    /// <summary>
    /// Every option combination over every pair of sample contents gives the same verdict as the model: BOM removal,
    /// optional per-line trimming, line endings rewritten to one form or kept, and the final line break dropped
    /// (<c>Optional</c>) or rejected on either side (<c>Forbidden</c>).
    /// </summary>
    [Test]
    public async Task AreEqual_EveryOptionCombination_AgreesWithTheModel(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var disagreements = new List<string>();
        var combinations =
            from mode in LineEndingModes
            from whitespace in Enum.GetValues<SnapshotTrailingWhitespace>()
            from newline in Enum.GetValues<SnapshotTrailingNewline>()
            from bom in Enum.GetValues<SnapshotBomHandling>()
            select new SnapshotOptions
            {
                LineEndingMode = mode,
                TrailingWhitespace = whitespace,
                TrailingNewline = newline,
                BomHandling = bom,
            };

        foreach (var options in combinations)
        {
            foreach (var actual in Samples)
            {
                foreach (var expected in Samples)
                {
                    var verdict = SnapshotComparer.AreEqual(actual, expected, options);
                    if (verdict != ModelAreEqual(actual, expected, options))
                        disagreements.Add($"{Describe(options)} actual={Escape(actual)} expected={Escape(expected)} verdict={verdict}");
                }
            }
        }

        await Assert.That(disagreements).IsEmpty();
    }

    /// <summary>
    /// A baseline accepted from some content matches that content on the next run, for every option combination
    /// except <c>Forbidden</c>, which rejects a trailing newline by design (the accepted file keeps it, so the policy
    /// still fails; see the evaluator tests). This is the round trip a user relies on: accept once, then run.
    /// </summary>
    [Test]
    public async Task AcceptThenRerun_EveryOptionCombination_Matches(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.Combine(Path.GetTempPath(), "model-rerun-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var failures = new List<string>();
            var index = 0;
            var combinations =
                from mode in LineEndingModes
                from whitespace in Enum.GetValues<SnapshotTrailingWhitespace>()
                from newline in new[] { SnapshotTrailingNewline.Required, SnapshotTrailingNewline.Optional }
                from bom in Enum.GetValues<SnapshotBomHandling>()
                select new SnapshotOptions
                {
                    LineEndingMode = mode,
                    TrailingWhitespace = whitespace,
                    TrailingNewline = newline,
                    BomHandling = bom,
                };

            foreach (var options in combinations)
            {
                foreach (var content in Samples)
                {
                    var paths = SnapshotFileResolver.ResolveByName(directory, "case-" + index++);
                    await SnapshotEvaluator.EvaluateAsync(content, paths, options, acceptModeOverride: true, cancellationToken: cancellationToken);
                    var rerun = await SnapshotEvaluator.EvaluateAsync(content, paths, options, acceptModeOverride: false, cancellationToken: cancellationToken);
                    if (!rerun.IsPass)
                        failures.Add($"{Describe(options)} content={Escape(content)} outcome={rerun.Outcome}");
                }
            }

            await Assert.That(failures).IsEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Content that ends with a blank line, under <c>Optional</c> (the <c>NormalizedLineEndings</c> preset), is written
    /// with its final line break, so the accepted baseline compares equal to the content. Without it the file would lose
    /// a second line break on the way back in and the snapshot could never be accepted into a passing state.
    /// </summary>
    [Test]
    public async Task Accept_OptionalWithTrailingBlankLine_WritesTheFinalLineBreak(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var trimmed = SnapshotOptions.NormalizedLineEndings with { TrailingWhitespace = SnapshotTrailingWhitespace.TrimTrailingPerLine };

        await Assert.That(await AcceptedTextAsync("a\r\n\r\n", SnapshotOptions.NormalizedLineEndings, cancellationToken)).IsEqualTo("a\n\n");
        await Assert.That(await AcceptedTextAsync("a\n \n", trimmed, cancellationToken)).IsEqualTo("a\n\n");
    }

    /// <summary>Under <c>Optional</c>, content that ends with a single line break is still written without it, as before.</summary>
    [Test]
    public async Task Accept_OptionalWithSingleFinalLineBreak_WritesWithoutIt(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(await AcceptedTextAsync("a\r\nb\r\n", SnapshotOptions.NormalizedLineEndings, cancellationToken)).IsEqualTo("a\nb");
        await Assert.That(await AcceptedTextAsync("a\nb", SnapshotOptions.NormalizedLineEndings, cancellationToken)).IsEqualTo("a\nb");
    }

    /// <summary>
    /// A line-ending mode outside the enum, such as the value the removed <c>IgnoreLineEndings</c> member had, is
    /// rejected with an <see cref="ArgumentOutOfRangeException"/> from the comparison, the normalization and the
    /// evaluator alike, never compared silently as some other mode, and nothing is written.
    /// </summary>
    [Test]
    public async Task UndefinedLineEndingMode_IsRejected(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = SnapshotOptions.Default with { LineEndingMode = (SnapshotLineEndingMode)3 };
        var directory = Path.Combine(Path.GetTempPath(), "model-undefined-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var paths = SnapshotFileResolver.ResolveByName(directory, "undefined");

            await Assert.That(() => SnapshotComparer.AreEqual("a\n", "a\n", options)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(async () => await SnapshotEvaluator.EvaluateAsync("a\n", paths, options, acceptModeOverride: true, cancellationToken: cancellationToken))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(Directory.GetFiles(directory)).IsEmpty();
            var exception = await Assert.That(() => SnapshotComparer.Normalize("a\n", options)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(exception!.Message).Contains("Unknown LineEndingMode value: 3");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Accepts <paramref name="content"/> into a fresh baseline and returns the text the baseline file holds.</summary>
    private static async Task<string> AcceptedTextAsync(string content, SnapshotOptions options, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "model-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var paths = SnapshotFileResolver.ResolveByName(directory, "written");
            await SnapshotEvaluator.EvaluateAsync(content, paths, options, acceptModeOverride: true, cancellationToken: cancellationToken);
            return await File.ReadAllTextAsync(paths.ExpectedFilePath, cancellationToken);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool ModelAreEqual(string actual, string expected, SnapshotOptions options)
    {
        var normalizedActual = Model(actual, options);
        var normalizedExpected = Model(expected, options);
        var forbidden = options.TrailingNewline is SnapshotTrailingNewline.Forbidden
            && (EndsWithBreak(normalizedActual) || EndsWithBreak(normalizedExpected));
        return !forbidden && string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal);
    }

    private static bool EndsWithBreak(string text) => text.Length > 0 && text[^1] is '\n' or '\r';

    private static string Model(string content, SnapshotOptions options)
    {
        if (options.BomHandling is SnapshotBomHandling.StripBom && content.StartsWith(Bom, StringComparison.Ordinal))
            content = content[Bom.Length..];

        var parts = Tokenize(content);
        var transformsLines = options.LineEndingMode is not SnapshotLineEndingMode.Ordinal
            || options.TrailingWhitespace is SnapshotTrailingWhitespace.TrimTrailingPerLine
            || options.TrailingNewline is SnapshotTrailingNewline.Optional;
        if (!transformsLines)
            return string.Concat(parts.Select(part => part.Text + part.Terminator));

        var result = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            var text = options.TrailingWhitespace is SnapshotTrailingWhitespace.TrimTrailingPerLine ? parts[i].Text.TrimEnd() : parts[i].Text;
            var terminator = parts[i].Terminator;
            if (terminator.Length > 0)
            {
                var isLast = i == parts.Count - 1;
                if (isLast && options.TrailingNewline is SnapshotTrailingNewline.Optional)
                    terminator = string.Empty;
                else if (options.LineEndingMode is SnapshotLineEndingMode.NormalizeToLF)
                    terminator = "\n";
                else if (options.LineEndingMode is SnapshotLineEndingMode.NormalizeToCRLF)
                    terminator = "\r\n";
            }

            result.Append(text).Append(terminator);
        }

        return result.ToString();
    }

    private static List<(string Text, string Terminator)> Tokenize(string content)
    {
        var parts = new List<(string Text, string Terminator)>();
        var text = new StringBuilder();
        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];
            if (c is '\n')
            {
                parts.Add((text.ToString(), "\n"));
                text.Clear();
                i++;
            }
            else if (c is '\r')
            {
                var crlf = i + 1 < content.Length && content[i + 1] is '\n';
                parts.Add((text.ToString(), crlf ? "\r\n" : "\r"));
                text.Clear();
                i += crlf ? 2 : 1;
            }
            else
            {
                text.Append(c);
                i++;
            }
        }

        if (text.Length > 0)
            parts.Add((text.ToString(), string.Empty));
        return parts;
    }

    private static string Describe(SnapshotOptions options)
        => $"{options.LineEndingMode}/{options.TrailingWhitespace}/{options.TrailingNewline}/{options.BomHandling}";

    private static string Escape(string text)
        => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace(Bom, "<BOM>", StringComparison.Ordinal);
}
