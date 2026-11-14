using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// What the assertion does with a custom scrubber that breaks its contract. A scrubber that returns
/// <see langword="null"/> is named in the failure, like a normalizer that returns <see langword="null"/>, instead
/// of surfacing later as an argument error about the actual content that the caller never passed.
/// </summary>
[Timeout(15_000)]
internal sealed class ScrubberContractTests
{
    /// <summary>A scrubber in the middle of a chain that returns <see langword="null"/> is named in the exception.</summary>
    [Test]
    public async Task ScrubberReturnsNull_ThrowsNamingTheScrubber(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = CreateTempDirectory();
        try
        {
            var expected = Path.Combine(directory, "null-scrubber.expected.txt");

            var exception = await Assert.That(async () => await Assert.That("text\n")
                    .MatchesSnapshot()
                    .AtPath(expected)
                    .WithScrubber(new NullReturningScrubber())
                    .WithScrubber(Scrubbers.Default))
                .Throws<InvalidOperationException>();

            await Assert.That(exception!.Message).IsEqualTo("The scrubber NullReturningScrubber returned null; a scrubber must return a string.");
            await Assert.That(Directory.GetFiles(directory)).IsEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scrubber-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>A scrubber that violates the contract by returning <see langword="null"/>.</summary>
    private sealed class NullReturningScrubber : SnapshotScrubber
    {
        /// <inheritdoc/>
        public override string Apply(string input, SnapshotScrubberState state) => null!;
    }
}
