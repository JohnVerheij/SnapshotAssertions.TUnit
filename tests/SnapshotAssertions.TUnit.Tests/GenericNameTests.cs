using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;

namespace SnapshotAssertions.TUnit.Tests;

/// <summary>
/// Pins a documented limitation of the default name: the name uses the simple class name, so every instantiation of a
/// generic test class resolves to the same file, with the arity marker of the class name in it. A generic test class
/// that snapshots content which depends on its type argument names each snapshot explicitly instead.
/// </summary>
[GenerateGenericTest(typeof(int))]
[GenerateGenericTest(typeof(string))]
[Timeout(15_000)]
internal sealed class GenericNameTests<T>
{
    /// <summary>Both instantiations (<see cref="int"/> and <see cref="string"/>) resolve the same default file name.</summary>
    [Test]
    public async Task DefaultName_IsTheSameForEveryTypeArgument(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = Path.GetFileName(SnapshotAssertionImpl.ResolvePaths(null, null, acceptModeOverride: false).ExpectedFilePath);

        await Assert.That(typeof(T)).IsNotNull();
        await Assert.That(name).IsEqualTo("GenericNameTests`1." + nameof(DefaultName_IsTheSameForEveryTypeArgument) + SnapshotFileResolver.ExpectedExtension);
    }

    /// <summary>An explicit name tells the instantiations apart: each resolves exactly the name it was given, which here
    /// contains its own type argument (<c>Generic.Int32</c> and <c>Generic.String</c>), so the two files differ.</summary>
    [Test]
    public async Task WithName_TellsTheTypeArgumentsApart(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = Path.GetFileName(SnapshotAssertionImpl.ResolvePaths(null, "Generic." + typeof(T).Name, acceptModeOverride: false).ExpectedFilePath);

        await Assert.That(name).IsEqualTo("Generic." + typeof(T).Name + SnapshotFileResolver.ExpectedExtension);
    }
}
