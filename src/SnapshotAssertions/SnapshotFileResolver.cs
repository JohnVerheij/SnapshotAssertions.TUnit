using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SnapshotAssertions;

/// <summary>
/// Path resolution for snapshot files. Computes the expected file path given a directory and
/// either an explicit name or a (test class, test method) pair, plus the matching
/// <c>.actual.txt</c> sibling path.
/// </summary>
/// <remarks>
/// All paths returned are absolute. The resolver does not touch the filesystem; the caller is
/// responsible for ensuring the directory exists when writing the actual file.
/// </remarks>
public static class SnapshotFileResolver
{
    /// <summary>The file extension used for committed expected baselines.</summary>
    public const string ExpectedExtension = ".expected.txt";

    /// <summary>The file extension used for transient actual content written on mismatch or
    /// no-baseline.</summary>
    public const string ActualExtension = ".actual.txt";

    /// <summary>The default subdirectory (relative to the test binary's directory) where
    /// snapshot files are read from and written to.</summary>
    public const string DefaultSnapshotsFolder = "Snapshots";

    /// <summary>
    /// Resolves the expected and actual file paths for a snapshot identified by an explicit
    /// name, located under <paramref name="snapshotsDirectory"/>.
    /// </summary>
    /// <param name="snapshotsDirectory">The absolute path to the directory containing snapshot
    /// files.</param>
    /// <param name="snapshotName">The base name (without extension). Must be a valid file-name
    /// component on the host platform; path separators are rejected.</param>
    /// <returns>The expected and actual paths.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="snapshotsDirectory"/> is empty or whitespace, or
    /// <paramref name="snapshotName"/> contains a path separator or is empty / whitespace.
    /// </exception>
    public static SnapshotPaths ResolveByName(string snapshotsDirectory, string snapshotName)
    {
        ArgumentNullException.ThrowIfNull(snapshotsDirectory);
        ArgumentNullException.ThrowIfNull(snapshotName);

        // Reject empty/whitespace directory explicitly. Without this, Path.Combine("", name)
        // returns just `name`, and Path.GetFullPath then resolves it against the process
        // working directory: which is almost never what the caller intended and produces
        // mysterious file-not-found behavior far from the call site.
        if (string.IsNullOrWhiteSpace(snapshotsDirectory))
            throw new ArgumentException("Snapshots directory must be non-empty.", nameof(snapshotsDirectory));

        if (string.IsNullOrWhiteSpace(snapshotName))
            throw new ArgumentException("Snapshot name must be non-empty.", nameof(snapshotName));

        if (snapshotName.IndexOfAny(['/', '\\']) >= 0
            || snapshotName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "Snapshot name must not contain path separators or invalid file-name characters.",
                nameof(snapshotName));
        }

        var expected = Path.GetFullPath(Path.Combine(snapshotsDirectory, snapshotName + ExpectedExtension));
        var actual = Path.GetFullPath(Path.Combine(snapshotsDirectory, snapshotName + ActualExtension));
        return new SnapshotPaths(expected, actual);
    }

    /// <summary>
    /// Resolves the expected and actual file paths for a snapshot identified by a (test class,
    /// test method) pair, optionally augmented with a hash of the test method's arguments
    /// for parameterized tests. The base name is constructed as
    /// <c>{testClassName}.{testMethodName}</c> when <paramref name="testMethodArguments"/> is
    /// <see langword="null"/> or empty, or <c>{testClassName}.{testMethodName}.{argsHash}</c>
    /// otherwise.
    /// </summary>
    /// <param name="snapshotsDirectory">The absolute path to the directory containing snapshot
    /// files.</param>
    /// <param name="testClassName">The simple test class name (no namespace).</param>
    /// <param name="testMethodName">The test method name.</param>
    /// <param name="testMethodArguments">The arguments passed to the parameterized test
    /// invocation, or <see langword="null"/> for non-parameterized tests. Each argument is
    /// stringified with <see cref="object.ToString"/> under
    /// <see cref="CultureInfo.InvariantCulture"/> contracts at the call site, joined with
    /// <c>"|"</c>, and hashed with SHA-256; the first 8 hex characters of the hash are
    /// appended to the base name. The hash is stable across runs for the same argument
    /// values, so each parameterized variant gets its own distinct snapshot file.</param>
    /// <returns>The expected and actual paths.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="testClassName"/> or <paramref name="testMethodName"/> is empty or contains
    /// invalid file-name characters.
    /// </exception>
    public static SnapshotPaths ResolveByTest(
        string snapshotsDirectory,
        string testClassName,
        string testMethodName,
        IReadOnlyList<object?>? testMethodArguments = null)
    {
        ArgumentNullException.ThrowIfNull(testClassName);
        ArgumentNullException.ThrowIfNull(testMethodName);

        if (string.IsNullOrWhiteSpace(testClassName))
            throw new ArgumentException("Test class name must be non-empty.", nameof(testClassName));
        if (string.IsNullOrWhiteSpace(testMethodName))
            throw new ArgumentException("Test method name must be non-empty.", nameof(testMethodName));

        var name = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", testClassName, testMethodName);

        if (testMethodArguments is { Count: > 0 })
        {
            var hash = HashArguments(testMethodArguments);
            name = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", name, hash);
        }

        return ResolveByName(snapshotsDirectory, name);
    }

    private static string HashArguments(IReadOnlyList<object?> args)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < args.Count; i++)
        {
            if (i > 0)
                sb.Append('|');
            sb.Append(StringifyArg(args[i]));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        // First 4 bytes = 8 hex characters. SHA-256 first-bytes is well-distributed and the
        // 32-bit-equivalent space (~4 billion) is more than enough for typical parameterized
        // test argument sets.
        return Convert.ToHexString(hash, 0, 4);
    }

    private static string StringifyArg(object? arg)
    {
        if (arg is null)
            return "null";

        // A string is enumerable but must stringify verbatim, not as a bracketed char list.
        if (arg is string text)
            return text;

        // Format with InvariantCulture for IFormattable types (DateTime, decimal, double,
        // numeric types, TimeSpan, etc.). Without this, the same arguments produce different
        // hashes on machines with different current cultures (e.g., decimal "1.5" on en-US
        // vs "1,5" on nl-NL), breaking baseline portability across developer machines and CI.
        if (arg is IFormattable formattable)
            return formattable.ToString(format: null, formatProvider: CultureInfo.InvariantCulture);

        // Expand a collection element-by-element. An array or list has no value-based ToString
        // (it returns the type name, e.g. "System.Int32[]"), so without this every collection
        // argument of a given type hashes identically and distinct parameterized variants collide
        // onto one snapshot file. Bracketing makes the expansion unambiguous and recursing handles
        // nested collections.
        if (arg is IEnumerable enumerable)
            return StringifyEnumerable(enumerable);

        // Fall back to the type's own ToString for non-IFormattable, non-enumerable references. The
        // hash contract documents that callers using custom types are responsible for providing a
        // stable ToString, so calling object.ToString here is intentional (not a culture-leak risk).
        // Meziantou MA0107 warns generically against object.ToString; this is the one case in the
        // resolver where it is the correct call.
        return Convert.ToString(arg, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string StringifyEnumerable(IEnumerable enumerable)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        var first = true;
        foreach (var item in enumerable)
        {
            if (!first)
                sb.Append(',');
            sb.Append(StringifyArg(item));
            first = false;
        }

        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Resolves the expected and actual file paths from an explicit absolute or relative file
    /// path to the expected file. The path is normalized to absolute form against the current
    /// working directory if relative.
    /// </summary>
    /// <param name="expectedFilePath">The path to the expected file (absolute or relative).</param>
    /// <returns>The expected and actual paths. The actual path is the expected path with the
    /// <see cref="ExpectedExtension"/> suffix replaced by <see cref="ActualExtension"/>; if
    /// the path does not end in the expected extension, <see cref="ActualExtension"/> is
    /// appended to the expected path's stem.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expectedFilePath"/> is <see langword="null"/>.</exception>
    public static SnapshotPaths ResolveByFile(string expectedFilePath)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);

        var absoluteExpected = Path.GetFullPath(expectedFilePath);

        string actual;
        if (absoluteExpected.EndsWith(ExpectedExtension, StringComparison.Ordinal))
        {
            var stem = absoluteExpected[..^ExpectedExtension.Length];
            actual = stem + ActualExtension;
        }
        else
        {
            // Caller used an unconventional expected-file extension (e.g., ".verified.txt"
            // legacy from a prior tool, or ".json" for JSON-flavoured snapshots). Sibling the
            // actual file by inserting ".actual" before the extension, mirroring the expected
            // file's shape.
            var dir = Path.GetDirectoryName(absoluteExpected) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(absoluteExpected);
            var ext = Path.GetExtension(absoluteExpected);
            actual = Path.Combine(dir, name + ".actual" + ext);
        }

        return new SnapshotPaths(absoluteExpected, actual);
    }

    /// <summary>
    /// Returns the default snapshots directory, derived as
    /// <c>{baseDirectory}/{DefaultSnapshotsFolder}</c>.
    /// </summary>
    /// <param name="baseDirectory">The directory the snapshots folder lives under (typically
    /// the test binary's <c>AppContext.BaseDirectory</c>).</param>
    /// <returns>The absolute path to the default snapshots directory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseDirectory"/> is <see langword="null"/>.</exception>
    public static string GetDefaultSnapshotsDirectory(string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        return Path.GetFullPath(Path.Combine(baseDirectory, DefaultSnapshotsFolder));
    }

    /// <summary>
    /// Resolves the <em>source-tree</em> snapshots directory: the committable
    /// <c>Snapshots/</c> folder that lives next to the test project file, rather than the
    /// runtime copy under <c>bin/</c>. Used by accept-mode so that an accepted baseline is
    /// written where it is committed and read from, not into the build output where it is
    /// discarded on the next clean.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The read path resolves baselines from <c>{AppContext.BaseDirectory}/Snapshots</c>: the
    /// build copies <c>Snapshots/**/*.expected.txt</c> from the project directory into
    /// <c>bin/</c> via the package's include glob. The accept write target is resolved by
    /// walking up from <paramref name="startDirectory"/> (typically
    /// <see cref="AppContext.BaseDirectory"/>) to the nearest ancestor that contains a
    /// <c>*.csproj</c> file, then appending <see cref="DefaultSnapshotsFolder"/>. That ancestor
    /// is the directory the build's include glob is relative to, so the accept write lands in
    /// the exact folder the next build copies back into <c>bin/</c>.
    /// </para>
    /// <para>
    /// When no ancestor project file can be found (for example, a single-file publish where the
    /// source tree is not present), the method returns <see langword="null"/>: the caller falls
    /// back to the runtime directory so accept-mode still produces a file, even if it is not in
    /// the source tree.
    /// </para>
    /// </remarks>
    /// <param name="startDirectory">The directory to start the upward search from (typically
    /// the test binary's <c>AppContext.BaseDirectory</c>).</param>
    /// <returns>The absolute path to the source-tree snapshots directory, or
    /// <see langword="null"/> if no ancestor project directory could be located.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="startDirectory"/> is <see langword="null"/>.</exception>
    public static string? TryResolveSourceSnapshotsDirectory(string startDirectory)
    {
        ArgumentNullException.ThrowIfNull(startDirectory);

        var projectDirectory = FindAncestorProjectDirectory(startDirectory);
        return projectDirectory is null
            ? null
            : Path.GetFullPath(Path.Combine(projectDirectory, DefaultSnapshotsFolder));
    }

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> and returns the first ancestor directory
    /// (inclusive) that contains at least one <c>*.csproj</c> file, or <see langword="null"/>
    /// if the filesystem root is reached without finding one.
    /// </summary>
    /// <param name="startDirectory">The directory to begin the search from.</param>
    /// <returns>The nearest ancestor project directory, or <see langword="null"/>.</returns>
    private static string? FindAncestorProjectDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            // EnumerateFiles is lazy: it stops at the first match instead of materializing the
            // whole listing, so the common case (a project file in the first ancestor that has
            // one) touches the directory once and returns immediately.
            using var matches = current.EnumerateFiles("*.csproj", SearchOption.TopDirectoryOnly).GetEnumerator();
            if (matches.MoveNext())
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
