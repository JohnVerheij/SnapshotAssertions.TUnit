using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SnapshotAssertions;

namespace SnapshotAssertions.Tests;

/// <summary>
/// Pins the path-construction logic in <see cref="SnapshotFileResolver"/>. Resolves are pure
/// (no IO) and produce absolute paths regardless of input.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SnapshotFileResolverTests
{
    private static readonly object?[] ArgsOneTwo = [new[] { 1, 2 }];
    private static readonly object?[] ArgsOneTwoCopy = [new[] { 1, 2 }];
    private static readonly object?[] ArgsThreeFour = [new[] { 3, 4 }];
    private static readonly object?[] ArgsNestedA = [new[] { new[] { 1 }, new[] { 2 } }];
    private static readonly object?[] ArgsNestedB = [new[] { new[] { 1 }, new[] { 9 } }];
    private static readonly object?[] ArgsMixed = [1, "two", null];
    private static readonly object?[] ArgsMixedCopy = [1, "two", null];
    private static readonly object?[] ArgsMixedOther = [2, "two", null];
    private static readonly object?[] ArgsCustom = [new CustomArg()];
    private static readonly object?[] ArgsRawToken = ["custom-token"];
    private static readonly object?[] ArgsNullToString = [new NullToStringArg(text: null)];
    private static readonly object?[] ArgsEmptyString = [string.Empty];

    /// <summary>ResolveByName builds <c>{dir}/{name}.expected.txt</c> and the actual sibling.</summary>
    [Test]
    public async Task ResolveByName_ReturnsExpectedAndActualSibling(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var paths = SnapshotFileResolver.ResolveByName(dir, "MyName");

        await Assert.That(paths.ExpectedFilePath).EndsWith("MyName.expected.txt");
        await Assert.That(paths.ActualFilePath).EndsWith("MyName.actual.txt");
        await Assert.That(Path.GetDirectoryName(paths.ExpectedFilePath))
            .IsEqualTo(Path.GetDirectoryName(paths.ActualFilePath));
    }

    private static readonly object?[] MethodArgsFive = [5];

    private static readonly string ClassArgsDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-class-args"));

    private static string NameOf(SnapshotPaths paths) =>
        Path.GetFileName(paths.ExpectedFilePath)[..^SnapshotFileResolver.ExpectedExtension.Length];

    /// <summary>Two class-level argument rows get two different names.</summary>
    [Test]
    public async Task ResolveByTest_ClassArguments_DifferentRowsGetDifferentNames(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var a = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { "alpha" }, "M", null);
        var b = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { "beta" }, "M", null);
        var again = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { "alpha" }, "M", null);

        await Assert.That(a.ExpectedFilePath).IsNotEqualTo(b.ExpectedFilePath);
        await Assert.That(a.ExpectedFilePath).IsEqualTo(again.ExpectedFilePath);
        await Assert.That(NameOf(a)).Matches("^C\\.[0-9A-F]{8}\\.M$");
    }

    /// <summary>Method arguments are appended after the method name, and class and method hashes are independent.</summary>
    [Test]
    public async Task ResolveByTest_ClassAndMethodArguments_BothHashesPresent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var both = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { 1 }, "M", new object?[] { 2 });
        var classOnly = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { 1 }, "M", null);
        var methodOnly = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", "M", new object?[] { 2 });

        await Assert.That(NameOf(both)).Matches("^C\\.[0-9A-F]{8}\\.M\\.[0-9A-F]{8}$");
        await Assert.That(NameOf(both)).StartsWith(NameOf(classOnly));
        await Assert.That(NameOf(both)).EndsWith(NameOf(methodOnly)["C.".Length..]);
    }

    /// <summary>Null, empty and fixture-only class arguments keep the name of the four-parameter overload.</summary>
    [Test]
    public async Task ResolveByTest_NoValueLikeClassArguments_KeepsLegacyName(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var legacy = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", "M", MethodArgsFive);

        await Assert.That(SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", null, "M", MethodArgsFive).ExpectedFilePath)
            .IsEqualTo(legacy.ExpectedFilePath);
        await Assert.That(SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", Array.Empty<object?>(), "M", MethodArgsFive).ExpectedFilePath)
            .IsEqualTo(legacy.ExpectedFilePath);
        await Assert.That(SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { new object() }, "M", MethodArgsFive).ExpectedFilePath)
            .IsEqualTo(legacy.ExpectedFilePath);
        await Assert.That(NameOf(SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { new object() }, "M", null)))
            .IsEqualTo("C.M");
    }

    /// <summary>A fixture object next to value arguments does not change the hash of the values.</summary>
    [Test]
    public async Task ResolveByTest_MixedFixtureAndValueClassArguments_HashesOnlyValues(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var valuesOnly = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { "x", 3 }, "M", null);
        var mixed = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { new object(), "x", new object(), 3 }, "M", null);

        await Assert.That(mixed.ExpectedFilePath).IsEqualTo(valuesOnly.ExpectedFilePath);
    }

    /// <summary>A null class argument is value-like and is hashed.</summary>
    [Test]
    public async Task ResolveByTest_NullClassArgument_IsHashed(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var withNull = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { null }, "M", null);

        await Assert.That(NameOf(withNull)).Matches("^C\\.[0-9A-F]{8}\\.M$");
    }

    /// <summary>A collection of value-like elements is hashed by its content; one non-value-like element skips the
    /// whole collection.</summary>
    [Test]
    public async Task ResolveByTest_CollectionClassArguments_HashedByContentOrSkippedWhole(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var oneTwo = new[] { 1, 2 };
        var oneThree = new[] { 1, 3 };
        var justOne = new[] { 1 };
        var one = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { oneTwo }, "M", null);
        var other = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { oneThree }, "M", null);
        var nested = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { new object?[] { justOne, "a", null } }, "M", null);
        var skippedMixed = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { new object?[] { 1, new object() } }, "M", null);
        var skippedNested = SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", new object?[] { new object?[] { new object?[] { new object() } } }, "M", null);

        await Assert.That(one.ExpectedFilePath).IsNotEqualTo(other.ExpectedFilePath);
        await Assert.That(NameOf(nested)).Matches("^C\\.[0-9A-F]{8}\\.M$");
        await Assert.That(NameOf(skippedMixed)).IsEqualTo("C.M");
        await Assert.That(NameOf(skippedNested)).IsEqualTo("C.M");
    }

    /// <summary>The five-parameter overload validates its names like the four-parameter one.</summary>
    [Test]
    public void ResolveByTest_ClassArgumentsOverload_RejectsBadNames(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentNullException>(() => SnapshotFileResolver.ResolveByTest(ClassArgsDir, null!, null, "M", null));
        Assert.Throws<ArgumentNullException>(() => SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", null, null!, null));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest(ClassArgsDir, " ", null, "M", null));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest(ClassArgsDir, "C", null, " ", null));
    }

    /// <summary>ResolveByTest joins class.method as the base name.</summary>
    [Test]
    public async Task ResolveByTest_JoinsClassAndMethod(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var paths = SnapshotFileResolver.ResolveByTest(dir, "MyClass", "MyMethod");

        await Assert.That(paths.ExpectedFilePath).EndsWith("MyClass.MyMethod.expected.txt");
        await Assert.That(paths.ActualFilePath).EndsWith("MyClass.MyMethod.actual.txt");
    }

    /// <summary>ResolveByFile preserves the explicit path and siblings the actual file.</summary>
    [Test]
    public async Task ResolveByFile_PreservesExpectedPath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var explicitPath = Path.Combine(dir, "Custom.expected.txt");
        var paths = SnapshotFileResolver.ResolveByFile(explicitPath);

        await Assert.That(paths.ExpectedFilePath).IsEqualTo(Path.GetFullPath(explicitPath));
        await Assert.That(paths.ActualFilePath).IsEqualTo(Path.GetFullPath(Path.Combine(dir, "Custom.actual.txt")));
    }

    /// <summary>ResolveByFile tolerates non-canonical extensions by inserting <c>.actual</c>
    /// before the extension on the actual file.</summary>
    [Test]
    public async Task ResolveByFile_NonCanonicalExtension_SiblingsActualPath(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var explicitPath = Path.Combine(dir, "Custom.json");
        var paths = SnapshotFileResolver.ResolveByFile(explicitPath);

        await Assert.That(paths.ActualFilePath).EndsWith("Custom.actual.json");
    }

    /// <summary>ResolveByFile appends <c>.actual</c> when the expected path has no extension.</summary>
    [Test]
    public async Task ResolveByFile_NoExtension_AppendsActual(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "snapshot-resolver-tests"));
        var explicitPath = Path.Combine(dir, "Custom");
        var paths = SnapshotFileResolver.ResolveByFile(explicitPath);

        await Assert.That(paths.ActualFilePath).IsEqualTo(Path.Combine(dir, "Custom.actual"));
    }

    /// <summary>Path-traversal characters in the snapshot name throw.</summary>
    [Test]
    public void ResolveByName_PathSeparator_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("snapshots", "../escape"));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("snapshots", "sub/path"));
    }

    /// <summary>Empty / whitespace name throws.</summary>
    [Test]
    public void ResolveByName_EmptyName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("snapshots", ""));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("snapshots", "   "));
    }

    /// <summary>Empty / whitespace snapshots-directory throws. Without this check, an empty
    /// directory string would Path.Combine into the bare snapshot name and Path.GetFullPath
    /// would resolve it against the process working directory: almost never what the
    /// caller intended.</summary>
    [Test]
    public void ResolveByName_EmptyDirectory_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName(string.Empty, "MyName"));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByName("   ", "MyName"));
    }

    /// <summary>GetDefaultSnapshotsDirectory appends the conventional <c>Snapshots</c>
    /// folder to the supplied base directory.</summary>
    [Test]
    public async Task GetDefaultSnapshotsDirectory_AppendsSnapshotsFolder(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var baseDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "base"));
        var snaps = SnapshotFileResolver.GetDefaultSnapshotsDirectory(baseDir);

        await Assert.That(snaps).IsEqualTo(Path.GetFullPath(Path.Combine(baseDir, "Snapshots")));
    }

    /// <summary>Empty / whitespace test-class name throws.</summary>
    [Test]
    public void ResolveByTest_EmptyTestClassName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest("snapshots", string.Empty, "M"));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest("snapshots", "   ", "M"));
    }

    /// <summary>Empty / whitespace test-method name throws.</summary>
    [Test]
    public void ResolveByTest_EmptyTestMethodName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest("snapshots", "C", string.Empty));
        Assert.Throws<ArgumentException>(() => SnapshotFileResolver.ResolveByTest("snapshots", "C", "   "));
    }

    /// <summary>Distinct collection arguments resolve to distinct snapshot files. A collection has no
    /// value-based ToString, so before recursive stringification every array argument of a type
    /// collapsed onto one file, colliding the parameterized variants.</summary>
    [Test]
    public async Task ResolveByTest_DistinctCollectionArgs_ProduceDistinctFiles(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsOneTwo);
        var second = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsThreeFour);
        await Assert.That(first.ExpectedFilePath).IsNotEqualTo(second.ExpectedFilePath);
    }

    /// <summary>Equal collection arguments (distinct instances, same values) resolve to the same file,
    /// so a re-run of the same parameterized variant reads back its own baseline.</summary>
    [Test]
    public async Task ResolveByTest_EqualCollectionArgs_ProduceSameFile(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsOneTwo);
        var second = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsOneTwoCopy);
        await Assert.That(first.ExpectedFilePath).IsEqualTo(second.ExpectedFilePath);
    }

    /// <summary>Nested collections are expanded recursively, so a difference in a nested element still
    /// produces a distinct file.</summary>
    [Test]
    public async Task ResolveByTest_NestedCollectionArgs_AreDistinguished(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsNestedA);
        var second = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsNestedB);
        await Assert.That(first.ExpectedFilePath).IsNotEqualTo(second.ExpectedFilePath);
    }

    /// <summary>A mix of scalar, string, and null arguments hashes deterministically and distinctly:
    /// the per-argument separator, the null sentinel, the verbatim string, and the invariant-culture
    /// formatting of the scalar all participate.</summary>
    [Test]
    public async Task ResolveByTest_MixedScalarStringNullArgs_DistinctAndStable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsMixed);
        var firstAgain = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsMixedCopy);
        var second = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsMixedOther);

        await Assert.That(first.ExpectedFilePath).IsEqualTo(firstAgain.ExpectedFilePath);
        await Assert.That(first.ExpectedFilePath).IsNotEqualTo(second.ExpectedFilePath);
    }

    /// <summary>A non-formattable, non-enumerable argument falls back to its own <c>ToString</c>.</summary>
    [Test]
    public async Task ResolveByTest_CustomObjectArg_UsesItsToString(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var withCustom = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsCustom);
        var withRaw = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsRawToken);

        // The custom type's ToString returns "custom-token", so it hashes the same as that string arg.
        await Assert.That(withCustom.ExpectedFilePath).IsEqualTo(withRaw.ExpectedFilePath);
    }

    /// <summary>An argument whose <c>ToString</c> returns <see langword="null"/> hashes as an empty string instead of
    /// throwing.</summary>
    [Test]
    public async Task ResolveByTest_ArgWithNullToString_HashesAsEmptyString(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var withNullToString = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsNullToString);
        var withEmpty = SnapshotFileResolver.ResolveByTest("snapshots", "C", "M", ArgsEmptyString);

        await Assert.That(withNullToString.ExpectedFilePath).IsEqualTo(withEmpty.ExpectedFilePath);
    }

    /// <summary>A root path has no parent directory (<c>Path.GetDirectoryName</c> returns <see langword="null"/>), so
    /// the actual sibling of a root expected path falls back to a relative <c>.actual</c> name instead of
    /// throwing.</summary>
    [Test]
    public async Task ResolveByFile_RootPath_HasNoDirectoryAndFallsBack(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var paths = SnapshotFileResolver.ResolveByFile(root);

        await Assert.That(paths.ExpectedFilePath).IsEqualTo(root);
        await Assert.That(paths.ActualFilePath).IsEqualTo(".actual");
    }

    private sealed class NullToStringArg(string? text)
    {
        public override string? ToString() => text;
    }

    private sealed class CustomArg
    {
        public override string ToString() => "custom-token";
    }

    /// <summary>TryResolveSourceSnapshotsDirectory walks up from a bin-like runtime directory to
    /// the ancestor holding the project file, then targets that project's <c>Snapshots</c>
    /// folder: the same directory the build's include glob is relative to (where an accepted
    /// baseline must land to be committed and read back).</summary>
    [Test]
    public async Task TryResolveSourceSnapshotsDirectory_FindsProjectAncestorSnapshots(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = CreateTempDirectory();
        try
        {
            // Lay out <root>/Proj/MyProj.csproj and a runtime-style <root>/Proj/bin/Release/net10.0.
            var projectDir = Path.Combine(root, "Proj");
            Directory.CreateDirectory(projectDir);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "MyProj.csproj"), "<Project/>", cancellationToken).ConfigureAwait(false);
            var binDir = Path.Combine(projectDir, "bin", "Release", "net10.0");
            Directory.CreateDirectory(binDir);

            var resolved = SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(binDir);

            await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(projectDir, "Snapshots")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>TryResolveSourceSnapshotsDirectory recognizes F# and Visual Basic project files as well as C#
    /// ones, so accept mode finds the project of an F# or Visual Basic test assembly.</summary>
    [Test]
    [Arguments("MyProj.fsproj")]
    [Arguments("MyProj.vbproj")]
    public async Task TryResolveSourceSnapshotsDirectory_NonCSharpProjectFile_FindsProjectAncestor(string projectFileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = CreateTempDirectory();
        try
        {
            var projectDir = Path.Combine(root, "Proj");
            var binDir = Path.Combine(projectDir, "bin", "Release", "net10.0");
            Directory.CreateDirectory(binDir);
            await File.WriteAllTextAsync(Path.Combine(projectDir, projectFileName), "<Project/>", cancellationToken).ConfigureAwait(false);

            var resolved = SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(binDir);

            await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(projectDir, "Snapshots")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>TryResolveSourceSnapshotsDirectory returns null when no ancestor project file
    /// exists, so accept-mode can fall back to the runtime directory rather than fabricating a
    /// bogus source path.</summary>
    [Test]
    public async Task TryResolveSourceSnapshotsDirectory_NoProjectAncestor_ReturnsNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = CreateTempDirectory();
        try
        {
            // A deep directory tree with no *.csproj anywhere above the start directory.
            var deep = Path.Combine(root, "a", "b", "c");
            Directory.CreateDirectory(deep);

            var resolved = SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(deep);

            await Assert.That(resolved).IsNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        // A unique temp root with no *.csproj anywhere above it (system temp dirs never carry
        // one), so the no-ancestor test sees a clean null and the found-ancestor test only sees
        // the project file this test plants.
        var dir = Path.Combine(Path.GetTempPath(), "snapshot-source-resolver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
