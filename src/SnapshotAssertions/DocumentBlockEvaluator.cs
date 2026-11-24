using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace SnapshotAssertions;

/// <summary>
/// Compares a block of lines with the lines between two marker lines of a document, such as a generated table in a
/// Markdown file, and writes the block there in accept mode. The rest of the document is never changed.
/// </summary>
/// <remarks>
/// <para>A marker is a whole line, compared ordinally without its line break. The begin marker must be on exactly one
/// line; the block ends at the first line after it that is the end marker. The block's lines are compared ordinally with
/// the lines between the two; the document's line breaks (carriage return and line feed, or line feed) are not part of
/// the comparison.</para>
/// <para>In accept mode a block that differs is written between the markers, through a temporary file in the document's
/// folder, so that a reader never sees a half-written document. The rewrite keeps the document's encoding, byte order
/// mark, and every byte outside the block; the block's lines get the line break of the begin marker's line. The rewrite
/// replaces the file, so it gets the permissions of a new file, and a document that is itself a symbolic link is not
/// written (the link would become a file): accept mode refuses it, and the caller passes the path of the file it leads
/// to. Accepting evaluations of one document run one at a time, within a process and across processes (such as the test
/// processes of the target frameworks of one <c>dotnet test</c> run), so that blocks of one document can be accepted in
/// parallel. The cross-process lock is a file in a folder of the temporary folder named after the user
/// (<c>SnapshotAssertions-&lt;user&gt;</c>, readable only by that user on Unix), kept for the next evaluation; an
/// evaluation waits up to about 30 seconds while another process holds it. An evaluation outside accept mode only reads,
/// and takes no lock.</para>
/// </remarks>
public static class DocumentBlockEvaluator
{
    /// <summary>
    /// Compares <paramref name="actualBlock"/> with the lines between the document's marker lines, and, when accept mode
    /// is active (<see cref="SnapshotAcceptMode.IsActive()"/>), writes it there when they differ.
    /// </summary>
    /// <param name="actualBlock">The block's text: its lines, each ended by a line feed or a carriage return and line feed;
    /// the last line break is optional. The empty text is a block of no lines.</param>
    /// <param name="documentPath">The document; a relative path is resolved against the current directory.</param>
    /// <param name="beginMarker">The line that opens the block: not empty, without a line break.</param>
    /// <param name="endMarker">The line that closes the block: not empty, without a line break, other than
    /// <paramref name="beginMarker"/>.</param>
    /// <param name="cancellationToken">A token to cancel the evaluation.</param>
    /// <returns>The result: <see cref="DocumentBlockOutcome.Matched"/>, <see cref="DocumentBlockOutcome.Mismatched"/>,
    /// <see cref="DocumentBlockOutcome.Accepted"/>, or <see cref="DocumentBlockOutcome.BlockNotFound"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> is empty or white space; a marker is empty or
    /// holds a line break; the markers are equal; or a line of <paramref name="actualBlock"/> is a marker or ends with a
    /// carriage return, so that the written block could not be read back.</exception>
    /// <exception cref="InvalidDataException">The document is not valid text in its encoding.</exception>
    /// <exception cref="IOException">The document could not be read or, in accept mode, replaced, or the lock file stayed
    /// held by another process (after repeated attempts); or, in accept mode, the document is a symbolic link.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to the document was refused (after repeated attempts, for the
    /// replacement).</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<DocumentBlockResult> EvaluateAsync(
        string actualBlock,
        string documentPath,
        string beginMarker,
        string endMarker,
        CancellationToken cancellationToken = default)
        => EvaluateAsync(actualBlock, documentPath, beginMarker, endMarker, SnapshotAcceptMode.IsActive(), cancellationToken);

    /// <summary>
    /// Compares <paramref name="actualBlock"/> with the lines between the document's marker lines, and, when
    /// <paramref name="acceptMode"/> is set, writes it there when they differ.
    /// </summary>
    /// <param name="actualBlock">The block's text: its lines, each ended by a line feed or a carriage return and line feed;
    /// the last line break is optional. The empty text is a block of no lines.</param>
    /// <param name="documentPath">The document; a relative path is resolved against the current directory.</param>
    /// <param name="beginMarker">The line that opens the block: not empty, without a line break.</param>
    /// <param name="endMarker">The line that closes the block: not empty, without a line break, other than
    /// <paramref name="beginMarker"/>.</param>
    /// <param name="acceptMode">Whether to write a block that differs (accept mode), whatever the environment says.</param>
    /// <param name="cancellationToken">A token to cancel the evaluation.</param>
    /// <returns>The result: <see cref="DocumentBlockOutcome.Matched"/>, <see cref="DocumentBlockOutcome.Mismatched"/>,
    /// <see cref="DocumentBlockOutcome.Accepted"/>, or <see cref="DocumentBlockOutcome.BlockNotFound"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> is empty or white space; a marker is empty or
    /// holds a line break; the markers are equal; or a line of <paramref name="actualBlock"/> is a marker or ends with a
    /// carriage return, so that the written block could not be read back.</exception>
    /// <exception cref="InvalidDataException">The document is not valid text in its encoding.</exception>
    /// <exception cref="IOException">The document could not be read or, in accept mode, replaced, or the lock file stayed
    /// held by another process (after repeated attempts); or, in accept mode, the document is a symbolic link.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to the document was refused (after repeated attempts, for the
    /// replacement).</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static async Task<DocumentBlockResult> EvaluateAsync(
        string actualBlock,
        string documentPath,
        string beginMarker,
        string endMarker,
        bool acceptMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actualBlock);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrEmpty(beginMarker);
        ArgumentException.ThrowIfNullOrEmpty(endMarker);
        ThrowIfLineBreak(beginMarker, nameof(beginMarker));
        ThrowIfLineBreak(endMarker, nameof(endMarker));
        if (string.Equals(beginMarker, endMarker, StringComparison.Ordinal))
            throw new ArgumentException("The end marker must differ from the begin marker.", nameof(endMarker));

        var block = DocumentText.SplitBlock(actualBlock);
        if (UnwritableLine(block, beginMarker, endMarker) is { } problem)
            throw new ArgumentException(problem, nameof(actualBlock));

        cancellationToken.ThrowIfCancellationRequested();

        var markers = new Markers(beginMarker, endMarker);
        var fullPath = Path.GetFullPath(documentPath);
        if (acceptMode && new FileInfo(fullPath).LinkTarget is not null)
        {
            throw new IOException(string.Format(
                CultureInfo.InvariantCulture,
                "The document '{0}' is a symbolic link. Accept mode replaces the file it writes, which would turn the link into a file: pass the path of the file the link leads to.",
                MessageText.EscapeLineBreaks(fullPath)));
        }

        return acceptMode
            ? await AcceptExclusiveAsync(block, fullPath, markers, cancellationToken).ConfigureAwait(false)
            : await CompareAsync(block, fullPath, markers, write: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Compares and writes while no other accepting evaluation of the document runs: it waits for the document's gate in
    /// this process, then opens the document's lock file without sharing, waiting a bounded time while another process
    /// holds it. The lock file stays, for the next evaluation: removing it after use would let two processes on a system
    /// with advisory locks hold the lock at once.
    /// </summary>
    /// <param name="block">The actual block's lines.</param>
    /// <param name="fullPath">The full path of the document.</param>
    /// <param name="markers">The begin and end markers.</param>
    /// <param name="cancellationToken">A token to cancel the evaluation.</param>
    /// <returns>The result.</returns>
    /// <exception cref="IOException">Another process held the lock file for the whole wait.</exception>
    private static async Task<DocumentBlockResult> AcceptExclusiveAsync(
        string[] block, string fullPath, Markers markers, CancellationToken cancellationToken)
    {
        var gate = DocumentGates.GetOrAdd(fullPath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SafeFileHandle? lockHandle = null;
        try
        {
            var lockPath = LockFilePath(fullPath);
            CreateLockFolder(Path.GetDirectoryName(lockPath)!);
            ExceptionDispatchInfo? denied = null;
            await SnapshotEvaluator.RetryTransientAsync(
                () =>
                {
                    // Another holder shows as an IOException, which passes by waiting; a refused access does not, so it
                    // ends the wait at once instead of after the whole wait.
                    try
                    {
                        lockHandle = File.OpenHandle(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        denied = ExceptionDispatchInfo.Capture(exception);
                    }
                },
                lockPath,
                LockAttempts,
                cancellationToken).ConfigureAwait(false);
            denied?.Throw();
            return await CompareAsync(block, fullPath, markers, write: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lockHandle?.Dispose();
            gate.Release();
        }
    }

    /// <summary>Compares the block with the document's, and writes it there when they differ and
    /// <paramref name="write"/> is set.</summary>
    /// <param name="block">The actual block's lines.</param>
    /// <param name="fullPath">The full path of the document.</param>
    /// <param name="markers">The begin and end markers.</param>
    /// <param name="write">Whether to write a block that differs (accept mode), which the caller serializes.</param>
    /// <param name="cancellationToken">A token to cancel the evaluation.</param>
    /// <returns>The result.</returns>
    private static async Task<DocumentBlockResult> CompareAsync(
        string[] block, string fullPath, Markers markers, bool write, CancellationToken cancellationToken)
    {
        DocumentText document;
        try
        {
            document = await DocumentText.ReadAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // Also a symbolic link whose target is missing, and a document deleted since the evaluation began.
            return DocumentBlockResult.NotFound(fullPath, "the document does not exist", markers.Begin, markers.End);
        }
        var begin = FindBegin(document, markers.Begin, out var problem);
        var end = begin < 0 ? -1 : FindEnd(document, begin, markers.End, out problem);
        if (end < 0)
            return DocumentBlockResult.NotFound(fullPath, problem!, markers.Begin, markers.End);

        var first = FirstDifference(document, begin, end, block);
        if (first < 0)
            return DocumentBlockResult.Matched(fullPath);

        if (write)
        {
            await WriteAtomicAsync(fullPath, document.ReplaceBetween(begin, end, block), cancellationToken).ConfigureAwait(false);
            return DocumentBlockResult.Accepted(fullPath);
        }

        var current = new string[end - begin - 1];
        for (var i = 0; i < current.Length; i++)
            current[i] = document.Content(begin + 1 + i);
        return DocumentBlockResult.Mismatched(fullPath, begin + 2 + first, LineDiffRenderer.RenderLines(current, block));
    }

    /// <summary>Finds the begin marker's line, which must be the only one.</summary>
    /// <param name="document">The document.</param>
    /// <param name="marker">The begin marker.</param>
    /// <param name="problem">When the result is -1, what is missing; otherwise <see langword="null"/>.</param>
    /// <returns>The 0-based line index, or -1.</returns>
    private static int FindBegin(DocumentText document, string marker, out string? problem)
    {
        var found = -1;
        for (var i = 0; i < document.Lines.Count; i++)
        {
            if (!document.LineEquals(i, marker))
                continue;

            if (found >= 0)
            {
                problem = string.Format(
                    CultureInfo.InvariantCulture,
                    "the begin marker is on more than one line (lines {0} and {1})",
                    found + 1,
                    i + 1);
                return -1;
            }

            found = i;
        }

        problem = found < 0 ? "no line of the document is the begin marker" : null;
        return found;
    }

    /// <summary>Finds the first end marker line after the begin marker's line.</summary>
    /// <param name="document">The document.</param>
    /// <param name="begin">The 0-based index of the begin marker's line.</param>
    /// <param name="marker">The end marker.</param>
    /// <param name="problem">When the result is -1, what is missing; otherwise <see langword="null"/>.</param>
    /// <returns>The 0-based line index, or -1.</returns>
    private static int FindEnd(DocumentText document, int begin, string marker, out string? problem)
    {
        for (var i = begin + 1; i < document.Lines.Count; i++)
        {
            if (document.LineEquals(i, marker))
            {
                problem = null;
                return i;
            }
        }

        problem = string.Format(
            CultureInfo.InvariantCulture,
            "no line after the begin marker (line {0}) is the end marker",
            begin + 1);
        return -1;
    }

    /// <summary>Returns the index, within the block, of the first line where the document's block and the actual block
    /// differ.</summary>
    /// <param name="document">The document.</param>
    /// <param name="begin">The 0-based index of the begin marker's line.</param>
    /// <param name="end">The 0-based index of the end marker's line.</param>
    /// <param name="block">The actual block's lines.</param>
    /// <returns>The 0-based index, which is the shorter block's length when it is a prefix of the other; -1 when the
    /// blocks are equal.</returns>
    private static int FirstDifference(DocumentText document, int begin, int end, string[] block)
    {
        var count = end - begin - 1;
        var shared = Math.Min(count, block.Length);
        for (var i = 0; i < shared; i++)
        {
            if (!document.LineEquals(begin + 1 + i, block[i]))
                return i;
        }

        return count == block.Length ? -1 : shared;
    }

    /// <summary>Writes a document through a temporary file in its folder, so that a reader never sees it half
    /// written.</summary>
    /// <param name="path">The full path of the document.</param>
    /// <param name="bytes">The document's new bytes.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>A task that completes when the document is replaced.</returns>
    private static async Task WriteAtomicAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var tempPath = Path.Join(Path.GetDirectoryName(path), Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            await SnapshotEvaluator.RetryTransientAsync(() => File.Move(tempPath, path, overwrite: true), path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // On success the temporary file was moved away; on a failed or canceled write or move this removes it.
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Returns the path of a document's lock file: in the folder <c>SnapshotAssertions-&lt;user&gt;</c> of the temporary
    /// folder (one per user, as the temporary folder can be shared between users), named after a hash of the document's
    /// full path in upper case, so that the spellings of one path on a case-insensitive file system share it.
    /// </summary>
    /// <param name="fullPath">The full path of the document.</param>
    /// <returns>The lock file's path.</returns>
    internal static string LockFilePath(string fullPath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant()));
        return Path.Combine(Path.GetTempPath(), "SnapshotAssertions-" + UserName(), "document-" + Convert.ToHexString(hash, 0, 16) + ".lock");
    }

    /// <summary>Returns the name of the user the lock folder is named after: the account name, else the <c>USER</c>
    /// variable (a process whose user ID has no account entry, as in some containers, has no account name), else
    /// <c>user</c>.</summary>
    /// <returns>The name.</returns>
    internal static string UserName()
        => UserName(Environment.UserName, Environment.GetEnvironmentVariable("USER"));

    /// <summary>Picks the user name from the account name and the <c>USER</c> variable.</summary>
    /// <param name="accountName">The account name, empty when the user has no account entry.</param>
    /// <param name="userVariable">The <c>USER</c> variable, or <see langword="null"/>.</param>
    /// <returns>The first of the two that is not empty, else <c>user</c>.</returns>
    internal static string UserName(string accountName, string? userVariable)
    {
        if (!string.IsNullOrEmpty(accountName))
            return accountName;

        return string.IsNullOrEmpty(userVariable) ? "user" : userVariable;
    }

    /// <summary>Creates the lock folder when it is missing: on Unix readable and writable by its user only, as the
    /// temporary folder there is usually shared.</summary>
    /// <param name="folder">The lock folder.</param>
    internal static void CreateLockFolder(string folder)
    {
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(folder);
        else
            Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Throws when a marker holds a line break.</summary>
    /// <param name="marker">The marker.</param>
    /// <param name="parameterName">The parameter it was passed as.</param>
    /// <exception cref="ArgumentException"><paramref name="marker"/> holds a line break.</exception>
    private static void ThrowIfLineBreak(string marker, string parameterName)
    {
        if (marker.AsSpan().IndexOfAny('\r', '\n') >= 0)
            throw new ArgumentException("A marker is one line: it must not hold a carriage return or line feed.", parameterName);
    }

    /// <summary>
    /// Finds a line of the block that could not be read back once written between the markers: a marker line, which
    /// would end the block early or repeat the begin marker, or a line that ends with a carriage return, which would read
    /// back as part of a line break.
    /// </summary>
    /// <param name="block">The actual block's lines.</param>
    /// <param name="beginMarker">The begin marker.</param>
    /// <param name="endMarker">The end marker.</param>
    /// <returns>What is wrong with the first such line, or <see langword="null"/>.</returns>
    private static string? UnwritableLine(string[] block, string beginMarker, string endMarker)
    {
        for (var i = 0; i < block.Length; i++)
        {
            var line = block[i];
            if (string.Equals(line, beginMarker, StringComparison.Ordinal) || string.Equals(line, endMarker, StringComparison.Ordinal))
                return string.Format(CultureInfo.InvariantCulture, "Line {0} of the actual block is a marker line; written between the markers, the block could not be read back.", i + 1);
            if (line.Length > 0 && line[^1] is '\r')
                return string.Format(CultureInfo.InvariantCulture, "Line {0} of the actual block ends with a carriage return that no line feed follows; written into the document, it would read back as part of the line break.", i + 1);
        }

        return null;
    }

    /// <summary>The attempts to open the lock file, 20 milliseconds apart: about 30 seconds, enough for another process to
    /// finish its own comparison and write of the document.</summary>
    private const int LockAttempts = 1500;

    /// <summary>One gate per document (full path, case-insensitive), so that accepting evaluations of a document within a
    /// process wait here rather than on its lock file.</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DocumentGates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The marker lines of a block.</summary>
    /// <param name="Begin">The line that opens the block.</param>
    /// <param name="End">The line that closes the block.</param>
    private readonly record struct Markers(string Begin, string End);
}
