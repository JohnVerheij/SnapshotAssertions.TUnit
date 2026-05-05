using System;
using System.IO;

namespace SnapshotAssertions;

/// <summary>
/// The outcome of a snapshot comparison, including paths to the expected baseline and (on
/// mismatch or no-baseline) the written actual file, plus a rendered line-based diff for
/// failure messages.
/// </summary>
public sealed record SnapshotResult
{
    private SnapshotResult(
        SnapshotMatchOutcome outcome,
        string expectedFilePath,
        string? actualFilePath,
        string? diff)
    {
        Outcome = outcome;
        ExpectedFilePath = expectedFilePath;
        ActualFilePath = actualFilePath;
        Diff = diff;
    }

    /// <summary>The classification of the comparison outcome.</summary>
    public SnapshotMatchOutcome Outcome { get; }

    /// <summary>Absolute path to the expected baseline file. Always populated.</summary>
    public string ExpectedFilePath { get; }

    /// <summary>Absolute path to the written <c>.actual.txt</c> file when
    /// <see cref="Outcome"/> is <see cref="SnapshotMatchOutcome.Mismatched"/> or
    /// <see cref="SnapshotMatchOutcome.NoBaseline"/>; <see langword="null"/> otherwise.</summary>
    public string? ActualFilePath { get; }

    /// <summary>Rendered line-based diff between expected and actual content when
    /// <see cref="Outcome"/> is <see cref="SnapshotMatchOutcome.Mismatched"/>;
    /// <see langword="null"/> otherwise. Format is not stable; intended for failure-message
    /// display, not programmatic parsing.</summary>
    public string? Diff { get; }

    /// <summary>Whether the comparison should be treated as a pass.
    /// <see cref="SnapshotMatchOutcome.Matched"/> and <see cref="SnapshotMatchOutcome.Accepted"/>
    /// pass; the others fail.</summary>
    public bool IsPass => Outcome is SnapshotMatchOutcome.Matched or SnapshotMatchOutcome.Accepted;

    /// <summary>Constructs a <see cref="SnapshotMatchOutcome.Matched"/> result.</summary>
    /// <param name="expectedFilePath">Absolute path to the matching expected file.</param>
    /// <returns>A passing result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expectedFilePath"/> is <see langword="null"/>.</exception>
    public static SnapshotResult Matched(string expectedFilePath)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);
        return new SnapshotResult(SnapshotMatchOutcome.Matched, expectedFilePath, actualFilePath: null, diff: null);
    }

    /// <summary>Constructs a <see cref="SnapshotMatchOutcome.Mismatched"/> result.</summary>
    /// <param name="expectedFilePath">Absolute path to the expected file.</param>
    /// <param name="actualFilePath">Absolute path to the written <c>.actual.txt</c> file.</param>
    /// <param name="diff">Rendered line-based diff for failure-message display.</param>
    /// <returns>A failing result describing the mismatch.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static SnapshotResult Mismatched(string expectedFilePath, string actualFilePath, string diff)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);
        ArgumentNullException.ThrowIfNull(actualFilePath);
        ArgumentNullException.ThrowIfNull(diff);
        return new SnapshotResult(SnapshotMatchOutcome.Mismatched, expectedFilePath, actualFilePath, diff);
    }

    /// <summary>Constructs a <see cref="SnapshotMatchOutcome.NoBaseline"/> result.</summary>
    /// <param name="expectedFilePath">Absolute path to where the expected file would be.</param>
    /// <param name="actualFilePath">Absolute path to the written <c>.actual.txt</c> file the
    /// caller can inspect and rename to <c>.expected.txt</c> to accept.</param>
    /// <returns>A failing result describing the missing baseline.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static SnapshotResult NoBaseline(string expectedFilePath, string actualFilePath)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);
        ArgumentNullException.ThrowIfNull(actualFilePath);
        return new SnapshotResult(SnapshotMatchOutcome.NoBaseline, expectedFilePath, actualFilePath, diff: null);
    }

    /// <summary>Constructs a <see cref="SnapshotMatchOutcome.Accepted"/> result.</summary>
    /// <param name="expectedFilePath">Absolute path to the now-overwritten expected file.</param>
    /// <returns>A passing result indicating the actual content was written over the baseline
    /// (accept-mode).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expectedFilePath"/> is <see langword="null"/>.</exception>
    public static SnapshotResult Accepted(string expectedFilePath)
    {
        ArgumentNullException.ThrowIfNull(expectedFilePath);
        return new SnapshotResult(SnapshotMatchOutcome.Accepted, expectedFilePath, actualFilePath: null, diff: null);
    }

    /// <summary>Renders a multi-line description of the result for use in assertion failure
    /// messages and diagnostic output. Includes the expected path, the actual path (when
    /// applicable), the diff (when applicable), and accept-flow guidance.</summary>
    /// <returns>A multi-line description; format is not stable.</returns>
    public string Describe()
    {
        using var writer = new StringWriter();
        WriteDescription(writer);
        return writer.ToString();
    }

    /// <summary>Writes the same description as <see cref="Describe"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">The destination text writer.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public void WriteDescription(TextWriter writer)
    {
        System.ArgumentNullException.ThrowIfNull(writer);
        switch (Outcome)
        {
            case SnapshotMatchOutcome.Matched:
                writer.Write("Snapshot matched: ");
                writer.WriteLine(ExpectedFilePath);
                break;
            case SnapshotMatchOutcome.Accepted:
                writer.Write("Snapshot accepted (SNAPSHOT_ACCEPT=1): ");
                writer.WriteLine(ExpectedFilePath);
                break;
            case SnapshotMatchOutcome.NoBaseline:
                writer.WriteLine("Snapshot baseline does not exist.");
                writer.Write("  Expected: ");
                writer.WriteLine(ExpectedFilePath);
                writer.Write("  Actual:   ");
                writer.WriteLine(ActualFilePath);
                writer.WriteLine();
                writer.WriteLine("Inspect the actual file and rename it to .expected.txt to accept it as the baseline,");
                writer.WriteLine("or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.");
                break;
            case SnapshotMatchOutcome.Mismatched:
                writer.WriteLine("Snapshot did not match the baseline.");
                writer.Write("  Expected: ");
                writer.WriteLine(ExpectedFilePath);
                writer.Write("  Actual:   ");
                writer.WriteLine(ActualFilePath);
                if (!string.IsNullOrEmpty(Diff))
                {
                    writer.WriteLine();
                    writer.Write(Diff);
                    if (!Diff.EndsWith('\n'))
                        writer.WriteLine();
                }
                writer.WriteLine();
                writer.WriteLine("To accept the change, rename the actual file over the expected file,");
                writer.WriteLine("or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.");
                break;
        }
    }
}
