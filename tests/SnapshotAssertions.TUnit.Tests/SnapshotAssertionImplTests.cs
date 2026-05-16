using System;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions.TUnit;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the one branch of <see cref="SnapshotAssertionImpl.ResolvePaths"/> that cannot be
/// reached from a normal <c>MatchesSnapshot</c> call inside a <c>[Test]</c> method: the
/// defensive throw when no explicit name or path is supplied AND there is no active TUnit
/// test context. <c>TestContext.Current</c> is an <c>AsyncLocal</c> set by the test
/// framework, so any call originating from a test method will see a non-null context. To
/// exercise the null branch the call must run on a thread whose <c>ExecutionContext</c>
/// did not flow from the parent test.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotAssertionImplTests
{
    [Test]
    public async Task ResolvePaths_NoTestContextNoExplicitNameOrPath_ThrowsInvalidOperation(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        InvalidOperationException? captured = null;

        // SuppressFlow stops the parent's ExecutionContext (and therefore the AsyncLocal
        // backing TestContext.Current) from being copied into the new thread. The Thread
        // is started inside the suppressed block; Thread.Join blocks the caller until the
        // captured exception is observed.
        using (ExecutionContext.SuppressFlow())
        {
            var thread = new Thread(() =>
            {
                try
                {
                    SnapshotAssertionImpl.ResolvePaths(null, null);
                }
                catch (InvalidOperationException ex)
                {
                    captured = ex;
                }
            });
            thread.Start();
            thread.Join();
        }

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).Contains("MatchesSnapshot()");
        await Assert.That(captured.Message).Contains("active TUnit test context");
    }
}
