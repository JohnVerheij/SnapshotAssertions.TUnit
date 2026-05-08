using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins the chain methods on <see cref="SnapshotAssertion"/>: <c>WithName</c>,
/// <c>WithOptions</c>, and the path-resolution flow that uses
/// <see cref="SnapshotFileResolver.GetDefaultSnapshotsDirectory"/>. The
/// <see cref="MatchesSnapshotTests"/> file covers <c>AtPath</c> + <c>MatchesSnapshotFile</c>;
/// this file fills the remaining chain-method gaps.
/// </summary>
[Category("Smoke")]
[Timeout(15_000)]
internal sealed class MatchesSnapshotChainTests
{
    /// <summary><c>WithName</c> resolves the file under
    /// <see cref="AppContext.BaseDirectory"/>/Snapshots/{name}.expected.txt.</summary>
    [Test]
    public async Task WithName_ResolvesFromDefaultSnapshotsDirectory(CancellationToken cancellationToken)
    {
        var name = "WithNameTest_" + Guid.NewGuid().ToString("N");
        await WithExpectedFileAsync(name, "hi-by-name\n", async () =>
        {
            await Assert.That("hi-by-name\n").MatchesSnapshot().WithName(name);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>MatchesSnapshot(name)</c> shorthand is equivalent to the
    /// <c>WithName</c> chain.</summary>
    [Test]
    public async Task ShorthandWithName_Matches(CancellationToken cancellationToken)
    {
        var name = "ShorthandName_" + Guid.NewGuid().ToString("N");
        await WithExpectedFileAsync(name, "shorthand-name\n", async () =>
        {
            await Assert.That("shorthand-name\n").MatchesSnapshot(name);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>MatchesSnapshot(name, options)</c> shorthand applies both the name
    /// override and the options.</summary>
    [Test]
    public async Task ShorthandWithNameAndOptions_NormalizedLineEndings_MatchesAcrossLineBreaks(CancellationToken cancellationToken)
    {
        var name = "ShorthandNameOptions_" + Guid.NewGuid().ToString("N");
        await WithExpectedFileAsync(name, "a\r\nb\r\n", async () =>
        {
            await Assert.That("a\nb\n").MatchesSnapshot(name, SnapshotOptions.NormalizedLineEndings);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>MatchesSnapshot(options)</c> shorthand applies the options without
    /// overriding the name (so the test-context-derived name is used).</summary>
    [Test]
    public async Task ShorthandWithOptionsOnly_UsesTestContextDerivedName(CancellationToken cancellationToken)
    {
        var className = nameof(MatchesSnapshotChainTests);
        var methodName = nameof(ShorthandWithOptionsOnly_UsesTestContextDerivedName);
        await WithExpectedFileAsync($"{className}.{methodName}", "ctx-name\n", async () =>
        {
            await Assert.That("ctx-name\n").MatchesSnapshot(SnapshotOptions.Default);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>MatchesSnapshotFile(path, options)</c> applies both the path override and
    /// the options.</summary>
    [Test]
    public async Task ShorthandFileWithOptions_NormalizedLineEndings_MatchesAcrossLineBreaks(CancellationToken cancellationToken)
    {
        using var dir = CreateTempDirectory();
        var expected = Path.Combine(dir, "diff.expected.txt");
        await File.WriteAllTextAsync(expected, "a\r\nb\r\n", cancellationToken).ConfigureAwait(false);

        await Assert.That("a\nb\n").MatchesSnapshotFile(expected, SnapshotOptions.NormalizedLineEndings);
    }

    /// <summary>The default <c>MatchesSnapshot()</c> entry uses the test-context-derived
    /// name (<c>{TestClassName}.{TestMethodName}</c>) under the default
    /// <c>Snapshots/</c> directory.</summary>
    [Test]
    public async Task DefaultPath_UsesTestContextDerivedName(CancellationToken cancellationToken)
    {
        var className = nameof(MatchesSnapshotChainTests);
        var methodName = nameof(DefaultPath_UsesTestContextDerivedName);
        await WithExpectedFileAsync($"{className}.{methodName}", "default-ctx\n", async () =>
        {
            await Assert.That("default-ctx\n").MatchesSnapshot();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>For parameterized tests, the resolver appends an args-hash to the snapshot
    /// name so each variant gets a distinct baseline file. This test exercises that path
    /// directly via <see cref="SnapshotFileResolver.ResolveByTest"/>.</summary>
    [Test]
    public async Task ResolveByTest_WithArgs_AppendsHashSuffix(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var dir = CreateTempDirectory();
        var paths = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 1, "foo" });

        await Assert.That(paths.ExpectedFilePath).EndsWith(".expected.txt");
        // The base "C.M" is followed by a dot then 8 hex chars before .expected.txt.
        var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(paths.ExpectedFilePath));
        await Assert.That(fileName).StartsWith("C.M.");
    }

    /// <summary>Args-hash is stable across calls with the same arguments.</summary>
    [Test]
    public async Task ResolveByTest_SameArgs_ProduceSameHash(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var dir = CreateTempDirectory();
        var pathsA = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 42, "hello", null });
        var pathsB = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 42, "hello", null });

        await Assert.That(pathsA.ExpectedFilePath).IsEqualTo(pathsB.ExpectedFilePath);
    }

    /// <summary>Different args produce different hashes.</summary>
    [Test]
    public async Task ResolveByTest_DifferentArgs_ProduceDifferentHashes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var dir = CreateTempDirectory();
        var pathsA = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 1 });
        var pathsB = SnapshotFileResolver.ResolveByTest(dir, "C", "M", new object?[] { 2 });

        await Assert.That(pathsA.ExpectedFilePath).IsNotEqualTo(pathsB.ExpectedFilePath);
    }

    /// <summary>Empty arg list does not add a hash suffix; the resolver behaves as if the
    /// args parameter were null.</summary>
    [Test]
    public async Task ResolveByTest_EmptyArgs_NoHashSuffix(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var dir = CreateTempDirectory();
        var pathsWithEmpty = SnapshotFileResolver.ResolveByTest(dir, "C", "M", System.Array.Empty<object?>());
        var pathsWithNull = SnapshotFileResolver.ResolveByTest(dir, "C", "M");

        await Assert.That(pathsWithEmpty.ExpectedFilePath).IsEqualTo(pathsWithNull.ExpectedFilePath);
    }

    /// <summary>The args-hash is culture-invariant: <see cref="IFormattable"/> arguments
    /// (DateTime, decimal, etc.) are stringified with <see cref="System.Globalization.CultureInfo.InvariantCulture"/>
    /// before hashing, so the same logical arguments produce the same hash on machines
    /// with different current cultures (e.g., en-US "1.5" vs nl-NL "1,5").</summary>
    [Test]
    public async Task ResolveByTest_IFormattableArgs_HashIsCultureInvariant(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var dir = CreateTempDirectory();
        var args = new object?[] { 1.5m, new System.DateTime(2026, 5, 5, 12, 0, 0, System.DateTimeKind.Utc) };

        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            var pathsEnUs = SnapshotFileResolver.ResolveByTest(dir, "C", "M", args);

            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
            var pathsNlNl = SnapshotFileResolver.ResolveByTest(dir, "C", "M", args);

            await Assert.That(pathsEnUs.ExpectedFilePath).IsEqualTo(pathsNlNl.ExpectedFilePath);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    /// <summary><c>WithScrubber</c> applies the supplied scrubber to the actual content
    /// before comparison against the baseline. Recurring volatile values (here a GUID) get
    /// stable indexed tokens (<c>&lt;guid:0&gt;</c>); the baseline contains the scrubbed form,
    /// not the raw GUID.</summary>
    [Test]
    public async Task WithScrubber_GuidScrubber_ScrubsBeforeComparison(CancellationToken cancellationToken)
    {
        var name = "WithScrubberGuid_" + Guid.NewGuid().ToString("N");
        var actualContent = "id=11111111-2222-3333-4444-555555555555 again=11111111-2222-3333-4444-555555555555\n";
        var expectedScrubbed = "id=<guid:0> again=<guid:0>\n";
        await WithExpectedFileAsync(name, expectedScrubbed, async () =>
        {
            await Assert.That(actualContent)
                .MatchesSnapshot()
                .WithName(name)
                .WithScrubber(Scrubbers.Guid);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Multiple <c>.WithScrubber</c> calls compose left-to-right and share state, so
    /// recurring values keep stable indexed tokens across the whole chain. Pins both the chain
    /// composition and the second-call branch of <c>WithScrubber</c> (the <c>_scrubbers ??=
    /// []</c> initialiser path is hit on the first call; the second call hits the
    /// already-initialised path).</summary>
    [Test]
    public async Task WithScrubber_TwoScrubbersChained_BothApplied(CancellationToken cancellationToken)
    {
        var name = "WithScrubberChained_" + Guid.NewGuid().ToString("N");
        var actualContent = "id=11111111-2222-3333-4444-555555555555 ts=2026-05-07T13:45:30Z\n";
        var expectedScrubbed = "id=<guid:0> ts=<iso8601:0>\n";
        await WithExpectedFileAsync(name, expectedScrubbed, async () =>
        {
            await Assert.That(actualContent)
                .MatchesSnapshot()
                .WithName(name)
                .WithScrubber(Scrubbers.Guid)
                .WithScrubber(Scrubbers.Iso8601Timestamp);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Argument validation: passing <see langword="null"/> as the scrubber throws
    /// <see cref="ArgumentNullException"/>.</summary>
    [Test]
    public async Task WithScrubber_NullScrubber_ThrowsArgumentNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The inner chain is built but not awaited: we only want WithScrubber's argument
        // validation to fire synchronously. Suppress the "must await" analyzer for the
        // builder line; the outer Assert.That(() => ...).Throws is what's being awaited.
#pragma warning disable TUnitAssertions0002
        var assertion = Assert.That("anything").MatchesSnapshot();
#pragma warning restore TUnitAssertions0002
        await Assert.That(() => assertion.WithScrubber(null!)).Throws<ArgumentNullException>();
    }

    private static async Task WithExpectedFileAsync(
        string snapshotName,
        string expectedContent,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        var snapshotsDir = SnapshotFileResolver.GetDefaultSnapshotsDirectory(AppContext.BaseDirectory);
        Directory.CreateDirectory(snapshotsDir);
        var path = Path.Combine(snapshotsDir, snapshotName + ".expected.txt");
        var actualPath = Path.Combine(snapshotsDir, snapshotName + ".actual.txt");
        await File.WriteAllTextAsync(path, expectedContent, cancellationToken).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
            if (File.Exists(actualPath))
                File.Delete(actualPath);
        }
    }

    private static TempDirectoryScope CreateTempDirectory() => new();

    /// <summary>
    /// Self-cleaning temp directory used by snapshot tests. Constructor allocates the dir
    /// under <see cref="Path.GetTempPath"/>; <see cref="Dispose"/> recursively removes it (best
    /// effort: swallows IO errors so a flaky teardown never masks the actual test failure).
    /// Implicit conversion to <see cref="string"/> keeps existing call sites readable
    /// (<c>Path.Combine(dir, ...)</c>) while still scoping cleanup to <c>using var</c>.
    /// </summary>
    private sealed class TempDirectoryScope : IDisposable
    {
        public string Path { get; }

        public TempDirectoryScope()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "matches-snapshot-chain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
                return;
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup. Leftover files in TEMP are harmless and the OS will
                // sweep them; we never want a teardown failure to hide an actual test failure.
            }
            catch (UnauthorizedAccessException)
            {
                // Same: best effort.
            }
        }

        public static implicit operator string(TempDirectoryScope scope)
        {
            ArgumentNullException.ThrowIfNull(scope);
            return scope.Path;
        }
    }
}
