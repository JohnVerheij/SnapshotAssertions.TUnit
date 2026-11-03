using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// End-to-end pin that class-level <c>[Arguments]</c> rows get their own default-named baseline files: the adapter
/// passes <c>TestDetails.TestClassArguments</c> to the resolver. Each row pre-writes the baseline at the name the
/// resolver computes and the default-named assertion must find exactly that file.
/// </summary>
[Arguments("alpha")]
[Arguments("beta")]
internal sealed class ClassArgumentsTests(string label)
{
    /// <summary>The row's baseline is found under the name that includes the class-argument hash.</summary>
    [Test]
    public async Task DefaultPath_WithClassArguments_UsesPerRowBaseline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotsDir = SnapshotFileResolver.GetDefaultSnapshotsDirectory(AppContext.BaseDirectory);
        Directory.CreateDirectory(snapshotsDir);
        var paths = SnapshotFileResolver.ResolveByTest(
            snapshotsDir,
            nameof(ClassArgumentsTests),
            new object?[] { label },
            nameof(DefaultPath_WithClassArguments_UsesPerRowBaseline),
            null);

        await Assert.That(Path.GetFileName(paths.ExpectedFilePath)).IsNotEqualTo(
            nameof(ClassArgumentsTests) + "." + nameof(DefaultPath_WithClassArguments_UsesPerRowBaseline) + SnapshotFileResolver.ExpectedExtension);

        var content = string.Create(CultureInfo.InvariantCulture, $"label={label}\n");
        await File.WriteAllTextAsync(paths.ExpectedFilePath, content, cancellationToken).ConfigureAwait(false);
        try
        {
            await Assert.That(content).MatchesSnapshot();
        }
        finally
        {
            if (File.Exists(paths.ExpectedFilePath))
                File.Delete(paths.ExpectedFilePath);
            if (File.Exists(paths.ActualFilePath))
                File.Delete(paths.ActualFilePath);
        }
    }
}
