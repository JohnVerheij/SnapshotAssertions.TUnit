using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins <see cref="DocumentBlockEvaluator"/>: how a document's block is found and compared, what accept mode writes
/// (and what it keeps byte for byte), what a document without a block gives, and the argument checks. Each test uses a
/// fresh temporary directory.
/// </summary>
[Timeout(15_000)]
internal sealed class DocumentBlockEvaluatorTests
{
    private const string Begin = "<!-- begin generated -->";
    private const string End = "<!-- end generated -->";
    private const string DocumentName = "document.md";

    private static readonly ConcurrentBag<string> TempDirectories = [];

    /// <summary>The block between the markers is the actual block: matched, and the document is not touched.</summary>
    [Test]
    public async Task Matching_LfDocument_ReturnsMatched(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"# Title\n{Begin}\na\nb\n{End}\ntail\n", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\nb\n", path, Begin, End, false, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
        await Assert.That(result.IsPass).IsTrue();
        await Assert.That(result.DocumentPath).IsEqualTo(path);
    }

    /// <summary>The document's line breaks are not compared: a CRLF document matches an LF block, and a CRLF block an LF
    /// document.</summary>
    [Test]
    public async Task Matching_LineBreaksDiffer_ReturnsMatched(CancellationToken cancellationToken)
    {
        var crlf = await CreateDocumentAsync($"{Begin}\r\na\r\nb\r\n{End}\r\n", cancellationToken);
        var lf = await CreateDocumentAsync($"{Begin}\na\nb\n{End}\n", cancellationToken);

        var crlfDocument = await DocumentBlockEvaluator.EvaluateAsync("a\nb\n", crlf, Begin, End, false, cancellationToken);
        var crlfBlock = await DocumentBlockEvaluator.EvaluateAsync("a\r\nb\r\n", lf, Begin, End, false, cancellationToken);

        await Assert.That(crlfDocument.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
        await Assert.That(crlfBlock.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
    }

    /// <summary>The last line break of the actual block is optional.</summary>
    [Test]
    public async Task Matching_ActualWithoutFinalLineBreak_ReturnsMatched(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\na\nb\n{End}\n", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\nb", path, Begin, End, false, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
    }

    /// <summary>The empty string is a block of no lines; a line break alone is one empty line.</summary>
    [Test]
    public async Task Matching_EmptyBlocks_ReturnMatched(CancellationToken cancellationToken)
    {
        var none = await CreateDocumentAsync($"{Begin}\n{End}\n", cancellationToken);
        var oneEmpty = await CreateDocumentAsync($"{Begin}\n\n{End}\n", cancellationToken);

        var noLines = await DocumentBlockEvaluator.EvaluateAsync("", none, Begin, End, false, cancellationToken);
        var emptyLine = await DocumentBlockEvaluator.EvaluateAsync("\n", oneEmpty, Begin, End, false, cancellationToken);

        await Assert.That(noLines.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
        await Assert.That(emptyLine.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
    }

    /// <summary>Leading and trailing empty lines of a block count, as in Markdown, where they separate the block from
    /// the comment lines around it.</summary>
    [Test]
    public async Task Matching_EmptyLinesAroundBlock_AreCompared(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\n\n| a |\n\n{End}\n", cancellationToken);

        var matched = await DocumentBlockEvaluator.EvaluateAsync("\n| a |\n\n", path, Begin, End, false, cancellationToken);
        var withoutTrailing = await DocumentBlockEvaluator.EvaluateAsync("\n| a |\n", path, Begin, End, false, cancellationToken);

        await Assert.That(matched.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
        await Assert.That(withoutTrailing.Outcome).IsEqualTo(DocumentBlockOutcome.Mismatched);
    }

    /// <summary>A carriage return that no line feed follows belongs to the line, in the document and in the block.</summary>
    [Test]
    public async Task Matching_LoneCarriageReturn_IsPartOfTheLine(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\na\rb\n{End}\n", cancellationToken);

        var matched = await DocumentBlockEvaluator.EvaluateAsync("a\rb\n", path, Begin, End, false, cancellationToken);
        var split = await DocumentBlockEvaluator.EvaluateAsync("a\nb\n", path, Begin, End, false, cancellationToken);

        await Assert.That(matched.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
        await Assert.That(split.Outcome).IsEqualTo(DocumentBlockOutcome.Mismatched);
    }

    /// <summary>A marker is a whole line: a line with more text, or with white space around the marker, is not one. The
    /// end marker is the first one after the begin marker; an end marker line before it does not count.</summary>
    [Test]
    public async Task Markers_AreWholeLines_EndIsFirstAfterBegin(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{End}\n {Begin}\n{Begin}\na\n{End}\nb\n{End}\n", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\n", path, Begin, End, false, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
    }

    /// <summary>Without an accept-mode override the evaluator consults the environment; the test session clears the
    /// accept variables, so a mismatch stays a mismatch.</summary>
    [Test]
    public async Task NoAcceptOverride_ConsultsEnvironment(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\nold\n{End}\n", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, cancellationToken: cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.Mismatched);
    }

    /// <summary>A mismatch leaves the document and its folder as they are, and names the line of the first
    /// difference.</summary>
    [Test]
    public async Task Mismatch_LeavesDocument_NamesFirstDifference(CancellationToken cancellationToken)
    {
        var content = $"# Title\n{Begin}\na\nb\nc\n{End}\n";
        var path = await CreateDocumentAsync(content, cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\nx\nc\n", path, Begin, End, false, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.Mismatched);
        await Assert.That(result.IsPass).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo(content);
        await Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!).Length).IsEqualTo(1);
        await Assert.That(Lf(result.Describe())).IsEqualTo(
            "Document block did not match.\n"
            + $"  Document: {path}:4 (the first difference)\n"
            + "  Diff:     '-' lines are the document's, '+' lines the actual block's\n"
            + "\n"
            + " a\n"
            + "-b\n"
            + "+x\n"
            + " c\n"
            + "\n"
            + "To write the actual block into the document, set SNAPSHOT_ACCEPT=1 (in a non-CI shell) and run the test again.\n");
    }

    /// <summary>An actual block that extends the document's block differs at the end marker's line, where its next line
    /// would go; a shorter one at its own end.</summary>
    [Test]
    public async Task Mismatch_DifferentLength_NamesLineAfterSharedPart(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\na\n{End}\n", cancellationToken);

        var longer = await DocumentBlockEvaluator.EvaluateAsync("a\nb\n", path, Begin, End, false, cancellationToken);
        var shorter = await DocumentBlockEvaluator.EvaluateAsync("", path, Begin, End, false, cancellationToken);

        await Assert.That(longer.Describe()).Contains($"{path}:3 (the first difference)");
        await Assert.That(Lf(longer.Describe())).Contains(" a\n+b\n");
        await Assert.That(shorter.Describe()).Contains($"{path}:2 (the first difference)");
        await Assert.That(Lf(shorter.Describe())).Contains("\n-a\n");
    }

    /// <summary>A block of no lines and a block of one empty line differ, though their lines join to the same text: the
    /// diff shows the empty line as added or removed.</summary>
    [Test]
    public async Task Mismatch_NoLinesVersusOneEmptyLine_ShowsTheEmptyLine(CancellationToken cancellationToken)
    {
        var none = await CreateDocumentAsync($"{Begin}\n{End}\n", cancellationToken);
        var oneEmpty = await CreateDocumentAsync($"{Begin}\n\n{End}\n", cancellationToken);

        var added = await DocumentBlockEvaluator.EvaluateAsync("\n", none, Begin, End, false, cancellationToken);
        var removed = await DocumentBlockEvaluator.EvaluateAsync("", oneEmpty, Begin, End, false, cancellationToken);

        await Assert.That(added.Outcome).IsEqualTo(DocumentBlockOutcome.Mismatched);
        await Assert.That(Lf(added.Describe())).Contains("'+' lines the actual block's\n\n+\n\nTo write");
        await Assert.That(removed.Outcome).IsEqualTo(DocumentBlockOutcome.Mismatched);
        await Assert.That(Lf(removed.Describe())).Contains("'+' lines the actual block's\n\n-\n\nTo write");
    }

    /// <summary>Accept mode writes the block between the markers and keeps every other byte; no temporary file is left.</summary>
    [Test]
    public async Task Accept_WritesBlock_KeepsTheRest(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"# Title\n\n{Begin}\nold 1\nold 2\n{End}\n\ntail", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("new 1\nnew 2\nnew 3\n", path, Begin, End, true, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.Accepted);
        await Assert.That(result.IsPass).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(path, cancellationToken))
            .IsEqualTo($"# Title\n\n{Begin}\nnew 1\nnew 2\nnew 3\n{End}\n\ntail");
        await Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!).Length).IsEqualTo(1);
        await Assert.That(result.Describe()).IsEqualTo($"Document block accepted (SNAPSHOT_ACCEPT=1): {path}{Environment.NewLine}");
    }

    /// <summary>The written lines get the begin marker line's line break, in a document that mixes them; the lines
    /// outside the block keep theirs.</summary>
    [Test]
    public async Task Accept_MixedLineBreaks_UsesTheBeginLinesBreak(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"head\n{Begin}\r\nold\n{End}\nx\r\n", cancellationToken);

        await DocumentBlockEvaluator.EvaluateAsync("a\nb\n", path, Begin, End, true, cancellationToken);

        await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo($"head\n{Begin}\r\na\r\nb\r\n{End}\nx\r\n");
    }

    /// <summary>An end marker on the last line, without a line break, stays without one; an empty block removes the
    /// lines between the markers.</summary>
    [Test]
    public async Task Accept_EmptyBlock_EndMarkerLastLine(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\nold\n{End}", cancellationToken);

        await DocumentBlockEvaluator.EvaluateAsync("", path, Begin, End, true, cancellationToken);

        await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo($"{Begin}\n{End}");
    }

    /// <summary>A UTF-8 document with a byte order mark keeps it, and the bytes outside the block are those it had.</summary>
    [Test]
    public async Task Accept_Utf8WithByteOrderMark_KeepsIt(CancellationToken cancellationToken)
    {
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var path = await CreateDocumentBytesAsync([.. utf8Bom.GetPreamble(), .. utf8Bom.GetBytes($"é\n{Begin}\nold\n{End}\n")], cancellationToken);

        await DocumentBlockEvaluator.EvaluateAsync("ü\n", path, Begin, End, true, cancellationToken);

        byte[] expected = [.. utf8Bom.GetPreamble(), .. utf8Bom.GetBytes($"é\n{Begin}\nü\n{End}\n")];
        await Assert.That(await File.ReadAllBytesAsync(path, cancellationToken)).IsEquivalentTo(expected);
    }

    /// <summary>A UTF-16 document is compared as text and rewritten in UTF-16 with its byte order mark.</summary>
    [Test]
    public async Task Accept_Utf16_KeepsEncodingAndByteOrderMark(CancellationToken cancellationToken)
    {
        var utf16 = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        var path = await CreateDocumentBytesAsync([.. utf16.GetPreamble(), .. utf16.GetBytes($"{Begin}\nold\n{End}\n")], cancellationToken);

        var mismatch = await DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, false, cancellationToken);
        await DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, true, cancellationToken);

        byte[] expected = [.. utf16.GetPreamble(), .. utf16.GetBytes($"{Begin}\nnew\n{End}\n")];
        await Assert.That(mismatch.Outcome).IsEqualTo(DocumentBlockOutcome.Mismatched);
        await Assert.That(await File.ReadAllBytesAsync(path, cancellationToken)).IsEquivalentTo(expected);
    }

    /// <summary>Accept mode with a block that matches does not write: the document keeps its bytes and its time.</summary>
    [Test]
    public async Task Accept_Matching_DoesNotWrite(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\r\nsame\r\n{End}\r\n", cancellationToken);
        var written = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, written);

        var result = await DocumentBlockEvaluator.EvaluateAsync("same\n", path, Begin, End, true, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
        await Assert.That(File.GetLastWriteTimeUtc(path)).IsEqualTo(written);
        await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo($"{Begin}\r\nsame\r\n{End}\r\n");
    }

    /// <summary>Two blocks of one document accepted at the same time are both written: evaluations of a document run one
    /// at a time.</summary>
    [Test]
    public async Task Accept_TwoBlocksInParallel_BothWritten(CancellationToken cancellationToken)
    {
        const string Begin2 = "<!-- begin second -->";
        var path = await CreateDocumentAsync($"{Begin}\nold\n{End}\n{Begin2}\nold\n{End}\n", cancellationToken);

        for (var round = 0; round < 20; round++)
        {
            var text = round.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await Task.WhenAll(
                DocumentBlockEvaluator.EvaluateAsync("first " + text + "\n", path, Begin, End, true, cancellationToken),
                DocumentBlockEvaluator.EvaluateAsync("second " + text + "\n", path, Begin2, End, true, cancellationToken));

            await Assert.That(await File.ReadAllTextAsync(path, cancellationToken))
                .IsEqualTo($"{Begin}\nfirst {text}\n{End}\n{Begin2}\nsecond {text}\n{End}\n");
        }
    }

    /// <summary>While another process holds the document's lock file (here a handle of this process, which the lock file
    /// refuses alike), an accepting evaluation waits, and writes once it is released.</summary>
    [Test]
    public async Task Accept_LockFileHeldBriefly_WaitsThenWrites(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\nold\n{End}\n", cancellationToken);
        var holder = HoldLockFile(path);
        Task<DocumentBlockResult> evaluation;
        await using (holder)
        {
            evaluation = DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, true, cancellationToken);
            // Longer than a second, the budget of the other file operations: the lock waits up to about 30 seconds.
            await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);

            await Assert.That(evaluation.IsCompleted).IsFalse();
            await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo($"{Begin}\nold\n{End}\n");
        }

        var result = await evaluation;

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.Accepted);
        await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo($"{Begin}\nnew\n{End}\n");
        // The lock file stays for the next evaluation: removing it after use would let two processes on a system with
        // advisory locks hold the lock at once.
        await Assert.That(File.Exists(DocumentBlockEvaluator.LockFilePath(path))).IsTrue();
    }

    /// <summary>While the lock file stays held, an accepting evaluation keeps waiting (up to about 30 seconds) and stops
    /// when its token is canceled, leaving the document as it was; the next accept then gets the lock. A comparison
    /// outside accept mode takes no lock, so it runs while the lock is held.</summary>
    [Test]
    public async Task Accept_LockFileHeldThroughout_WaitsUntilCanceled(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\nold\n{End}\n", cancellationToken);
        var holder = HoldLockFile(path);
        await using (holder)
        {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wait.CancelAfter(TimeSpan.FromMilliseconds(300));

            await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, true, wait.Token))
                .Throws<OperationCanceledException>();
            var comparison = await DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, false, cancellationToken);

            await Assert.That(comparison.Outcome).IsEqualTo(DocumentBlockOutcome.Mismatched);
            await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo($"{Begin}\nold\n{End}\n");
        }

        var accepted = await DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, true, cancellationToken);

        await Assert.That(accepted.Outcome).IsEqualTo(DocumentBlockOutcome.Accepted);
    }

    /// <summary>The lock file is in the temporary folder's <c>SnapshotAssertions-&lt;user&gt;</c> folder (a temporary
    /// folder can be shared between users), and two spellings of a path that differ only in case share it.</summary>
    [Test]
    public async Task LockFilePath_SharedAcrossCase(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(Path.GetTempPath(), "Docs", "Manual.md");

        var lockPath = DocumentBlockEvaluator.LockFilePath(path);

        await Assert.That(lockPath).IsEqualTo(DocumentBlockEvaluator.LockFilePath(path.ToUpperInvariant()));
        await Assert.That(Path.GetDirectoryName(lockPath)).IsEqualTo(Path.Combine(Path.GetTempPath(), "SnapshotAssertions-" + Environment.UserName));
        await Assert.That(Path.GetFileName(lockPath)).Matches("^document-[0-9A-F]{32}\\.lock$");
        await Assert.That(DocumentBlockEvaluator.LockFilePath(path + "x")).IsNotEqualTo(lockPath);
    }

    /// <summary>An empty block between the markers, the state a document has once the markers are added: the diff shows
    /// only the actual lines, as added, and names the end marker's line as the first difference.</summary>
    [Test]
    public async Task Mismatch_EmptyBlock_ShowsOnlyTheAddedLines(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"# Title\n{Begin}\n{End}\n", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\nb\n", path, Begin, End, false, cancellationToken);

        await Assert.That(Lf(result.Describe())).IsEqualTo(
            "Document block did not match.\n"
            + $"  Document: {path}:3 (the first difference)\n"
            + "  Diff:     '-' lines are the document's, '+' lines the actual block's\n"
            + "\n"
            + "+a\n"
            + "+b\n"
            + "\n"
            + "To write the actual block into the document, set SNAPSHOT_ACCEPT=1 (in a non-CI shell) and run the test again.\n");
    }

    /// <summary>A line with a carriage return inside it differs from the same text split in two, and the diff shows the
    /// lines as they are, without a hint about line endings (the document's line breaks are not compared).</summary>
    [Test]
    public async Task Mismatch_LoneCarriageReturn_ShowsTheLinesAsTheyAre(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{Begin}\na\rb\n{End}\n", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\nb\n", path, Begin, End, false, cancellationToken);

        await Assert.That(Lf(result.Describe())).Contains("\n-a\rb\n+a\n+b\n");
        await Assert.That(result.Describe()).DoesNotContain("line endings");
    }

    /// <summary>A line of the actual block that ends with a carriage return is refused: written into the document, the
    /// carriage return would read back as part of the line break, so the block would never match.</summary>
    [Test]
    public async Task BlockLineEndingWithCarriageReturn_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exception = await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("row1\r\nrow2\r", "doc.md", Begin, End, true, cancellationToken))
            .Throws<ArgumentException>();

        await Assert.That(exception!.ParamName).IsEqualTo("actualBlock");
        await Assert.That(exception.Message).StartsWith("Line 2 of the actual block ends with a carriage return");
    }

    /// <summary>A document that is itself a symbolic link is compared through the link, but accept mode refuses it
    /// before taking the lock (a rewrite would turn the link into a file): the link, its target and the lock folder stay
    /// as they were. A link whose target is missing is a document that does not exist. Skipped where the process may not
    /// create a symbolic link (Windows without developer mode); the Linux run covers it.</summary>
    [Test]
    public async Task SymbolicLink_ComparedNotWritten(CancellationToken cancellationToken)
    {
        var target = await CreateDocumentAsync($"{Begin}\nold\n{End}\n", cancellationToken);
        var folder = CreateTempDirectory();
        var link = Path.Combine(folder, "link.md");
        var dangling = Path.Combine(folder, "dangling.md");
        try
        {
            File.CreateSymbolicLink(link, target);
            File.CreateSymbolicLink(dangling, Path.Combine(folder, "missing.md"));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            Skip.Test("This process may not create a symbolic link: " + failure.Message);
        }

        var compared = await DocumentBlockEvaluator.EvaluateAsync("old\n", link, Begin, End, false, cancellationToken);
        var exception = await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("new\n", link, Begin, End, true, cancellationToken))
            .Throws<IOException>();
        var missing = await DocumentBlockEvaluator.EvaluateAsync("a\n", dangling, Begin, End, false, cancellationToken);

        await Assert.That(compared.Outcome).IsEqualTo(DocumentBlockOutcome.Matched);
        await Assert.That(exception!.Message).Contains("is a symbolic link");
        await Assert.That(new FileInfo(link).LinkTarget).IsEqualTo(target);
        await Assert.That(await File.ReadAllTextAsync(target, cancellationToken)).IsEqualTo($"{Begin}\nold\n{End}\n");
        await Assert.That(File.Exists(DocumentBlockEvaluator.LockFilePath(link))).IsFalse();
        await Assert.That(missing.Outcome).IsEqualTo(DocumentBlockOutcome.BlockNotFound);
        await Assert.That(missing.Describe()).StartsWith("Document block not found: the document does not exist.");
    }

    /// <summary>A document in a folder that does not exist is a document that does not exist.</summary>
    [Test]
    public async Task NotFound_MissingFolder(CancellationToken cancellationToken)
    {
        var path = Path.Combine(CreateTempDirectory(), "absent", DocumentName);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\n", path, Begin, End, false, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.BlockNotFound);
    }

    /// <summary>A lock file the process may not open fails the accept at once with the access error, rather than after
    /// the whole wait for another holder, and the document stays as it was. Skipped as root on Unix, which may open any
    /// file.</summary>
    [Test]
    public async Task Accept_LockFileAccessDenied_FailsAtOnce(CancellationToken cancellationToken)
    {
        Skip.When(!OperatingSystem.IsWindows() && string.Equals(Environment.UserName, "root", StringComparison.Ordinal), "Root may open any file.");
        var path = await CreateDocumentAsync($"{Begin}\nold\n{End}\n", cancellationToken);
        var lockPath = DocumentBlockEvaluator.LockFilePath(path);
        using (HoldLockFile(path))
        {
            // Create the lock file, then release it.
        }

        DenyAccess(lockPath);
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, true, cancellationToken))
                .Throws<UnauthorizedAccessException>();

            await Assert.That(clock.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
            await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo($"{Begin}\nold\n{End}\n");
        }
        finally
        {
            AllowAccess(lockPath);
        }
    }

    /// <summary>The lock folder is named after the account, else the <c>USER</c> variable, else <c>user</c>.</summary>
    [Test]
    public async Task LockFolder_NamedAfterTheUser(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await Assert.That(DocumentBlockEvaluator.UserName("alice", "bob")).IsEqualTo("alice");
        await Assert.That(DocumentBlockEvaluator.UserName("", "bob")).IsEqualTo("bob");
        await Assert.That(DocumentBlockEvaluator.UserName("", "")).IsEqualTo("user");
        await Assert.That(DocumentBlockEvaluator.UserName("", null)).IsEqualTo("user");
        await Assert.That(Path.GetFileName(Path.GetDirectoryName(DocumentBlockEvaluator.LockFilePath(Path.GetFullPath("doc.md")))))
            .IsEqualTo("SnapshotAssertions-" + DocumentBlockEvaluator.UserName());
    }

    /// <summary>A replacement that keeps failing (the target is a folder, on every platform) throws after the bounded
    /// retries, and the temporary file is removed, so the document's folder holds nothing new.</summary>
    [Test]
    public async Task WriteAtomic_ReplacementFails_RemovesTheTemporaryFile(CancellationToken cancellationToken)
    {
        var folder = CreateTempDirectory();
        var target = Path.Combine(folder, "document.md");
        Directory.CreateDirectory(target);

        var exception = await Assert.That(async () => await DocumentBlockEvaluator.WriteAtomicAsync(target, [1, 2, 3], cancellationToken))
            .Throws<Exception>();

        await Assert.That(exception is IOException or UnauthorizedAccessException).IsTrue();
        await Assert.That(Directory.GetFileSystemEntries(folder)).IsEquivalentTo([target]);
    }

    /// <summary>A document that does not exist: not found, also in accept mode, and nothing is created.</summary>
    [Test]
    public async Task NotFound_MissingDocument(CancellationToken cancellationToken)
    {
        var path = Path.Combine(CreateTempDirectory(), DocumentName);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\n", path, Begin, End, true, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.BlockNotFound);
        await Assert.That(result.IsPass).IsFalse();
        await Assert.That(File.Exists(path)).IsFalse();
        await Assert.That(Lf(result.Describe())).IsEqualTo(
            "Document block not found: the document does not exist.\n"
            + $"  Document:     {path}\n"
            + $"  Begin marker: {Begin}\n"
            + $"  End marker:   {End}\n"
            + "\n"
            + "A block is written only between marker lines the document has: add the begin marker on one line and the\n"
            + "end marker on a line after it, then set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to write the block.\n");
    }

    /// <summary>No line is the begin marker: not found, and accept mode does not write.</summary>
    [Test]
    public async Task NotFound_NoBeginMarker(CancellationToken cancellationToken)
    {
        var content = $"{Begin} \na\n{End}\n";
        var path = await CreateDocumentAsync(content, cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\n", path, Begin, End, true, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.BlockNotFound);
        await Assert.That(result.Describe()).StartsWith("Document block not found: no line of the document is the begin marker.");
        await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo(content);
    }

    /// <summary>The begin marker on two lines: not found, naming both lines.</summary>
    [Test]
    public async Task NotFound_BeginMarkerTwice(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"x\n{Begin}\na\n{End}\n{Begin}\nb\n{End}\n{Begin}\n", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\n", path, Begin, End, false, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.BlockNotFound);
        await Assert.That(result.Describe()).StartsWith("Document block not found: the begin marker is on more than one line (lines 2 and 5).");
    }

    /// <summary>No end marker after the begin marker: not found, naming the begin marker's line.</summary>
    [Test]
    public async Task NotFound_NoEndMarkerAfterBegin(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentAsync($"{End}\nx\n{Begin}\na\n", cancellationToken);

        var result = await DocumentBlockEvaluator.EvaluateAsync("a\n", path, Begin, End, true, cancellationToken);

        await Assert.That(result.Outcome).IsEqualTo(DocumentBlockOutcome.BlockNotFound);
        await Assert.That(result.Describe()).StartsWith("Document block not found: no line after the begin marker (line 3) is the end marker.");
    }

    /// <summary>A document that is not valid UTF-8 is refused before anything is compared or written.</summary>
    [Test]
    public async Task InvalidUtf8_Throws(CancellationToken cancellationToken)
    {
        var path = await CreateDocumentBytesAsync([.. Encoding.UTF8.GetBytes($"{Begin}\n"), 0xFF, 0x0A, .. Encoding.UTF8.GetBytes($"{End}\n")], cancellationToken);

        var exception = await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("a\n", path, Begin, End, true, cancellationToken))
            .Throws<InvalidDataException>();

        await Assert.That(exception!.Message).Contains(path);
        await Assert.That(exception.Message).Contains("not valid utf-8 text");
    }

    /// <summary>A read-only document fails the accept with the path in the message, keeps its content, and no temporary
    /// file is left. Windows only: elsewhere a read-only file can be replaced by a move.</summary>
    [Test]
    public async Task Accept_ReadOnlyDocument_ThrowsAndCleansUp(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Skip.Unless(OperatingSystem.IsWindows(), "A read-only file blocks a replace-move on Windows only.");
        var path = await CreateDocumentAsync($"{Begin}\nold\n{End}\n", cancellationToken);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            var exception = await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("new\n", path, Begin, End, true, cancellationToken))
                .Throws<UnauthorizedAccessException>();

            await Assert.That(exception!.Message).Contains($"Access to the file '{path}' failed after repeated attempts");
            await Assert.That(await File.ReadAllTextAsync(path, cancellationToken)).IsEqualTo($"{Begin}\nold\n{End}\n");
            await Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!).Length).IsEqualTo(1);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    /// <summary>A canceled token stops the evaluation before the document is read.</summary>
    [Test]
    public async Task CanceledToken_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("a\n", "doc.md", Begin, End, false, source.Token))
            .Throws<OperationCanceledException>();
    }

    /// <summary>Null arguments are refused, naming the parameter.</summary>
    [Test]
    [Arguments(0, "actualBlock")]
    [Arguments(1, "documentPath")]
    [Arguments(2, "beginMarker")]
    [Arguments(3, "endMarker")]
    public async Task NullArgument_Throws(int nullIndex, string parameterName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string?[] arguments = ["a\n", "doc.md", Begin, End];
        arguments[nullIndex] = null;

        var exception = await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync(arguments[0]!, arguments[1]!, arguments[2]!, arguments[3]!, false, cancellationToken))
            .Throws<ArgumentNullException>();

        await Assert.That(exception!.ParamName).IsEqualTo(parameterName);
    }

    /// <summary>An empty or white-space path, an empty marker, a marker with a line break, and equal markers are refused,
    /// naming the parameter.</summary>
    [Test]
    [Arguments(" ", Begin, End, "documentPath")]
    [Arguments("doc.md", "", End, "beginMarker")]
    [Arguments("doc.md", Begin, "", "endMarker")]
    [Arguments("doc.md", "a\rb", End, "beginMarker")]
    [Arguments("doc.md", Begin, "a\nb", "endMarker")]
    [Arguments("doc.md", Begin, Begin, "endMarker")]
    public async Task InvalidArgument_Throws(string documentPath, string beginMarker, string endMarker, string parameterName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exception = await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("a\n", documentPath, beginMarker, endMarker, false, cancellationToken))
            .Throws<ArgumentException>();

        await Assert.That(exception!.ParamName).IsEqualTo(parameterName);
    }

    /// <summary>An actual block with a marker line is refused: written between the markers, it could not be read back.</summary>
    [Test]
    [Arguments(Begin)]
    [Arguments(End)]
    public async Task BlockWithMarkerLine_Throws(string marker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exception = await Assert.That(async () => await DocumentBlockEvaluator.EvaluateAsync("a\n" + marker + "\n", "doc.md", Begin, End, true, cancellationToken))
            .Throws<ArgumentException>();

        await Assert.That(exception!.ParamName).IsEqualTo("actualBlock");
        await Assert.That(exception.Message).StartsWith("Line 2 of the actual block is a marker line");
    }

    /// <summary>The description escapes a line break in the path, so the path stays on its line; a matched result
    /// describes itself in one line; a null writer is refused.</summary>
    [Test]
    public async Task Describe_EscapesPath_NullWriterThrows(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mismatch = DocumentBlockResult.Mismatched("dir\nname.md", 7, "-a" + Environment.NewLine);
        var matched = DocumentBlockResult.Matched("dir\r\nname.md");

        await Assert.That(mismatch.Describe()).Contains("  Document: dir\\nname.md:7 (the first difference)");
        await Assert.That(matched.Describe()).IsEqualTo("Document block matched: dir\\r\\nname.md" + Environment.NewLine);
        await Assert.That(() => matched.WriteDescription(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Removes the temporary directories, and the lock files that accepting their documents left in the
    /// temporary folder (the evaluator keeps a document's lock file for the next evaluation).</summary>
    [After(Class)]
    public static void DeleteTempDirectories(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var directory in TempDirectories)
        {
            var lockPath = DocumentBlockEvaluator.LockFilePath(Path.Combine(directory, DocumentName));
            if (File.Exists(lockPath))
                File.Delete(lockPath);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void DenyAccess(string path)
    {
        if (OperatingSystem.IsWindows())
            File.SetAttributes(path, FileAttributes.ReadOnly);
        else
            File.SetUnixFileMode(path, UnixFileMode.UserRead);
    }

    private static void AllowAccess(string path)
    {
        if (OperatingSystem.IsWindows())
            File.SetAttributes(path, FileAttributes.Normal);
        else
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static FileStream HoldLockFile(string documentPath)
    {
        var lockPath = DocumentBlockEvaluator.LockFilePath(documentPath);
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private static async Task<string> CreateDocumentAsync(string content, CancellationToken cancellationToken)
        => await CreateDocumentBytesAsync(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content), cancellationToken);

    private static async Task<string> CreateDocumentBytesAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var path = Path.Combine(CreateTempDirectory(), DocumentName);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "document-block-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        TempDirectories.Add(directory);
        return directory;
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
