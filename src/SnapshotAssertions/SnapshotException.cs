using System;

namespace SnapshotAssertions;

/// <summary>
/// Exception for a test-framework adapter or a caller of <see cref="SnapshotEvaluator"/> to throw when a comparison
/// fails (mismatch or missing baseline). Carries the underlying <see cref="SnapshotResult"/> for programmatic
/// access; the <see cref="Exception.Message"/> is the same human-readable form rendered by
/// <see cref="SnapshotResult.Describe"/>.
/// </summary>
/// <remarks>
/// <para>Nothing in these packages throws it: the TUnit adapter reports a failed comparison as a failed TUnit
/// assertion (an <c>AssertionException</c> with the same description), not as a <see cref="SnapshotException"/>.</para>
/// <para>Not marked <c>[Serializable]</c>: legacy <c>BinaryFormatter</c> serialization is deprecated
/// since .NET 5 and unsupported in modern .NET; cross-AppDomain transfer of the underlying
/// <see cref="SnapshotResult"/> is not a supported scenario for this exception.</para>
/// </remarks>
public sealed class SnapshotException : Exception
{
    /// <summary>Initializes an exception with no result. Provided for the .NET exception
    /// constructor pattern (CA1032); production code should use
    /// <see cref="SnapshotException(SnapshotResult)"/>.</summary>
    public SnapshotException() : base("Snapshot comparison failed.") { }

    /// <summary>Initializes an exception with a custom message. Provided for the .NET
    /// exception constructor pattern (CA1032); production code should use
    /// <see cref="SnapshotException(SnapshotResult)"/> instead.</summary>
    /// <param name="message">The human-readable failure message.</param>
    public SnapshotException(string message) : base(message) { }

    /// <summary>Initializes an exception with a custom message and inner exception. Provided
    /// for the .NET exception constructor pattern (CA1032); production code should use
    /// <see cref="SnapshotException(SnapshotResult)"/> instead.</summary>
    /// <param name="message">The human-readable failure message.</param>
    /// <param name="innerException">The cause exception.</param>
    public SnapshotException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Initializes an exception describing a failed snapshot comparison.</summary>
    /// <param name="result">The failed comparison result. Must not be a passing outcome.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="result"/> represents a passing
    /// outcome; only failing outcomes warrant an exception.</exception>
    public SnapshotException(SnapshotResult result)
        : base(BuildMessage(result))
    {
        Result = result;
    }

    /// <summary>The result that triggered the exception, when constructed via
    /// <see cref="SnapshotException(SnapshotResult)"/>; otherwise <see langword="null"/>.</summary>
    public SnapshotResult? Result { get; }

    private static string BuildMessage(SnapshotResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsPass)
        {
            throw new ArgumentException(
                "SnapshotException must only be constructed for a failing SnapshotResult.",
                nameof(result));
        }

        return result.Describe();
    }
}
