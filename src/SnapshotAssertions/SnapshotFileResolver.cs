using System;
using System.Globalization;
using System.IO;

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
    /// <exception cref="ArgumentException"><paramref name="snapshotName"/> contains a path
    /// separator or is empty / whitespace.</exception>
    public static SnapshotPaths ResolveByName(string snapshotsDirectory, string snapshotName)
    {
        ArgumentNullException.ThrowIfNull(snapshotsDirectory);
        ArgumentNullException.ThrowIfNull(snapshotName);

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
    /// test method) pair. The base name is constructed as
    /// <c>{testClassName}.{testMethodName}</c>.
    /// </summary>
    /// <param name="snapshotsDirectory">The absolute path to the directory containing snapshot
    /// files.</param>
    /// <param name="testClassName">The simple test class name (no namespace).</param>
    /// <param name="testMethodName">The test method name.</param>
    /// <returns>The expected and actual paths.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="testClassName"/> or <paramref name="testMethodName"/> is empty or contains
    /// invalid file-name characters.
    /// </exception>
    public static SnapshotPaths ResolveByTest(string snapshotsDirectory, string testClassName, string testMethodName)
    {
        ArgumentNullException.ThrowIfNull(testClassName);
        ArgumentNullException.ThrowIfNull(testMethodName);

        if (string.IsNullOrWhiteSpace(testClassName))
            throw new ArgumentException("Test class name must be non-empty.", nameof(testClassName));
        if (string.IsNullOrWhiteSpace(testMethodName))
            throw new ArgumentException("Test method name must be non-empty.", nameof(testMethodName));

        var name = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", testClassName, testMethodName);
        return ResolveByName(snapshotsDirectory, name);
    }

    /// <summary>
    /// Resolves the expected and actual file paths from an explicit absolute or relative file
    /// path to the expected file. The path is normalised to absolute form against the current
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
}
