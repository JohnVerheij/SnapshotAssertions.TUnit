using System;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the four <see cref="SnapshotException"/> constructors. The result-based constructor
/// is the production path; the parameterless / message-only / message+inner constructors
/// exist to satisfy the .NET exception-constructor pattern (CA1032).
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotExceptionTests
{
    /// <summary>Default constructor produces a non-empty message and a null
    /// <see cref="SnapshotException.Result"/>.</summary>
    [Test]
    public async Task DefaultConstructor_ProducesMessageAndNullResult(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ex = new SnapshotException();

        await Assert.That(ex.Message).IsNotEmpty();
        await Assert.That(ex.Result).IsNull();
    }

    /// <summary>Message constructor preserves the supplied message.</summary>
    [Test]
    public async Task MessageConstructor_PreservesMessage(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ex = new SnapshotException("custom message");

        await Assert.That(ex.Message).IsEqualTo("custom message");
        await Assert.That(ex.Result).IsNull();
    }

    /// <summary>Message + inner exception constructor preserves both.</summary>
    [Test]
    public async Task MessageAndInnerExceptionConstructor_PreservesBoth(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inner = new InvalidOperationException("inner cause");
        var ex = new SnapshotException("outer message", inner);

        await Assert.That(ex.Message).IsEqualTo("outer message");
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
        await Assert.That(ex.Result).IsNull();
    }

    /// <summary>Result constructor with a failing result populates
    /// <see cref="SnapshotException.Result"/> and produces a message containing the
    /// expected and actual paths.</summary>
    [Test]
    public async Task ResultConstructor_WithFailingResult_PopulatesResultAndMessage(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = SnapshotResult.NoBaseline("/tmp/foo.expected.txt", "/tmp/foo.actual.txt");
        var ex = new SnapshotException(result);

        await Assert.That(ex.Result).IsSameReferenceAs(result);
        await Assert.That(ex.Message).Contains("/tmp/foo.expected.txt");
        await Assert.That(ex.Message).Contains("/tmp/foo.actual.txt");
    }

    /// <summary>Result constructor rejects a passing result: only failing outcomes warrant
    /// an exception, so a Matched or Accepted result throws <see cref="ArgumentException"/>
    /// at construction time.</summary>
    [Test]
    public void ResultConstructor_WithPassingResult_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => _ = new SnapshotException(SnapshotResult.Matched("/tmp/ok.expected.txt")));
        Assert.Throws<ArgumentException>(() => _ = new SnapshotException(SnapshotResult.Accepted("/tmp/ok.expected.txt")));
    }

    /// <summary>Result constructor rejects a null result with
    /// <see cref="ArgumentNullException"/>.</summary>
    [Test]
    public void ResultConstructor_WithNullResult_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentNullException>(() => _ = new SnapshotException((SnapshotResult)null!));
    }
}
