using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SnapshotAssertions;

/// <summary>
/// Reads a baseline file as text, recognizing a UTF-8, UTF-16 (LE or BE) or UTF-32 (LE or BE) byte-order mark and
/// decoding the rest of the file with the encoding it declares (UTF-8 when there is no mark).
/// </summary>
internal static class BaselineReader
{
    private const char Bom = '\uFEFF';

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UnicodeEncoding Utf16LittleEndian = new(bigEndian: false, byteOrderMark: false);
    private static readonly UnicodeEncoding Utf16BigEndian = new(bigEndian: true, byteOrderMark: false);
    private static readonly UTF32Encoding Utf32LittleEndian = new(bigEndian: false, byteOrderMark: false);
    private static readonly UTF32Encoding Utf32BigEndian = new(bigEndian: true, byteOrderMark: false);

    /// <summary>
    /// Reads <paramref name="path"/> and returns its text. A detected byte-order mark is consumed, and under
    /// <see cref="SnapshotBomHandling.PreserveBom"/> it is returned as a leading U+FEFF so the comparison sees it.
    /// </summary>
    /// <param name="path">The baseline file path.</param>
    /// <param name="bomHandling">How a byte-order mark at the start of the file is treated.</param>
    /// <param name="cancellationToken">Cancellation token propagated to the file read.</param>
    /// <returns>The decoded text of the file.</returns>
    internal static async Task<string> ReadAsync(string path, SnapshotBomHandling bomHandling, CancellationToken cancellationToken)
    {
        var bytes = await ReadAllBytesSharedAsync(path, cancellationToken).ConfigureAwait(false);
        var (encoding, markLength) = Detect(bytes);
        var text = encoding.GetString(bytes, markLength, bytes.Length - markLength);
        return markLength > 0 && bomHandling is SnapshotBomHandling.PreserveBom ? Bom + text : text;
    }

    /// <summary>
    /// Reads the whole file while allowing other evaluations to replace or delete it meanwhile. The default read
    /// shares the file for reading only, which on Windows makes another evaluation's replace-move of the same
    /// baseline fail while this one reads it (two test processes accepting one project baseline).
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="cancellationToken">Cancellation token propagated to the file read.</param>
    /// <returns>The bytes of the file.</returns>
    private static async Task<byte[]> ReadAllBytesSharedAsync(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer.ToArray();
        }
    }

    /// <summary>
    /// Identifies the encoding declared by the byte-order mark at the start of <paramref name="bytes"/>. The UTF-32
    /// little-endian mark is tested before the UTF-16 one because it starts with the same two bytes.
    /// </summary>
    /// <param name="bytes">The file content.</param>
    /// <returns>The encoding and the length in bytes of its mark (0 for UTF-8 without a mark).</returns>
    private static (Encoding Encoding, int MarkLength) Detect(byte[] bytes)
    {
        if (StartsWith(bytes, 0xFF, 0xFE, 0x00, 0x00))
            return (Utf32LittleEndian, 4);
        if (StartsWith(bytes, 0x00, 0x00, 0xFE, 0xFF))
            return (Utf32BigEndian, 4);
        if (StartsWith(bytes, 0xEF, 0xBB, 0xBF))
            return (Utf8, 3);
        if (StartsWith(bytes, 0xFF, 0xFE))
            return (Utf16LittleEndian, 2);
        if (StartsWith(bytes, 0xFE, 0xFF))
            return (Utf16BigEndian, 2);
        return (Utf8, 0);
    }

    private static bool StartsWith(byte[] bytes, params ReadOnlySpan<byte> prefix)
        => bytes.AsSpan().StartsWith(prefix);
}
