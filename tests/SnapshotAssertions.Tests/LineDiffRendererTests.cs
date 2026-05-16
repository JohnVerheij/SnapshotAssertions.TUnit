using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the line-by-line diff renderer's prefix conventions and truncation behavior.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class LineDiffRendererTests
{
    /// <summary>Identical inputs produce a diff with only context lines (no <c>-</c> or
    /// <c>+</c> markers).</summary>
    [Test]
    public async Task IdenticalInputs_OnlyContextLines(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diff = LineDiffRenderer.Render("a\nb\nc\n", "a\nb\nc\n");

        await Assert.That(diff).DoesNotContain("-");
        await Assert.That(diff).DoesNotContain("+");
    }

    /// <summary>Single-line difference produces one <c>-</c> and one <c>+</c> line.</summary>
    [Test]
    public async Task SingleLineDifference_EmitsMinusAndPlus(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diff = LineDiffRenderer.Render("expected\n", "actual\n");

        await Assert.That(diff).Contains("-expected");
        await Assert.That(diff).Contains("+actual");
    }

    /// <summary>When the number of differing lines exceeds the limit, the output is
    /// truncated with a count-summary footer.</summary>
    [Test]
    public async Task ManyDifferences_Truncated(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sb = new System.Text.StringBuilder();
        var sb2 = new System.Text.StringBuilder();
        for (var i = 0; i < 30; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"expected{i}\n");
            sb2.Append(CultureInfo.InvariantCulture, $"actual{i}\n");
        }

        var diff = LineDiffRenderer.Render(sb.ToString(), sb2.ToString());

        await Assert.That(diff).Contains("(truncated;");
        await Assert.That(diff).Contains("differing line(s)");
    }

    /// <summary>Null arguments throw.</summary>
    [Test]
    public void NullArguments_Throw(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentNullException>(() => LineDiffRenderer.Render(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => LineDiffRenderer.Render("x", null!));
    }

    /// <summary>Expected has more lines than actual: trailing expected-only lines render with
    /// a <c>-</c> prefix and no matching <c>+</c> line. Pins the <c>hasActual = false</c> ternary
    /// branch in <c>AccumulateDifferingTotal</c> and the <c>if (hasActual)</c> false branch in
    /// <c>EmitDifferingPair</c>.</summary>
    [Test]
    public async Task ExpectedLongerThanActual_TrailingExpectedLinesEmitMinusOnly(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diff = LineDiffRenderer.Render("a\nb\nc\n", "a\n");

        await Assert.That(diff).Contains("-b");
        await Assert.That(diff).Contains("-c");
        await Assert.That(diff).DoesNotContain("+b");
        await Assert.That(diff).DoesNotContain("+c");
    }

    /// <summary>Actual has more lines than expected: trailing actual-only lines render with a
    /// <c>+</c> prefix and no matching <c>-</c> line. Pins the <c>hasExpected = false</c>
    /// ternary branch and the <c>if (hasExpected && ...)</c> short-circuit-false path in
    /// <c>EmitDifferingPair</c>.</summary>
    [Test]
    public async Task ActualLongerThanExpected_TrailingActualLinesEmitPlusOnly(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diff = LineDiffRenderer.Render("a\n", "a\nb\nc\n");

        await Assert.That(diff).Contains("+b");
        await Assert.That(diff).Contains("+c");
        await Assert.That(diff).DoesNotContain("-b");
        await Assert.That(diff).DoesNotContain("-c");
    }
}
