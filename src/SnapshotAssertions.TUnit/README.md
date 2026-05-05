# SnapshotAssertions.TUnit

[![NuGet](https://img.shields.io/nuget/v/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![Downloads](https://img.shields.io/nuget/dt/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

TUnit-native text-snapshot assertions on top of TUnit's `[AssertionExtension]` source generator. AOT-compatible, trimmable, no reflection. Coexists with [Verify](https://github.com/VerifyTests/Verify); does not replace it for object-graph cases.

> **Full documentation, full options reference, design notes, and roadmap:** [github.com/JohnVerheij/SnapshotAssertions.TUnit](https://github.com/JohnVerheij/SnapshotAssertions.TUnit)

## Install

```
dotnet add package SnapshotAssertions.TUnit
```

`SnapshotAssertions` (the framework-agnostic core) comes transitively. **Requirements:** TUnit 1.43.2 or later, .NET 10.

## Quick start

```csharp
using SnapshotAssertions;
using PublicApiGenerator;

[Test]
public async Task Public_api_surface_matches_baseline()
{
    var assembly = typeof(MyLib.Foo).Assembly;
    var actual = ApiGenerator.GeneratePublicApi(assembly);

    await Assert.That(actual).MatchesSnapshot();
}
```

The default file resolver writes `Snapshots/{TestClassName}.{TestMethodName}.expected.txt`. On mismatch, `*.actual.txt` is written next to the expected file and the assertion failure includes both paths plus a line-based diff.

## Accept-changes workflow

Three modes, in order of preference:

1. **IDE diff-and-merge.** Most IDEs (Rider, VS Code) detect side-by-side `.expected.txt` and `.actual.txt` files and offer a diff-and-merge view.
2. **Manual `cp`.** `cp Snapshots/MyTest.actual.txt Snapshots/MyTest.expected.txt`.
3. **Bulk accept.** `SNAPSHOT_ACCEPT=1 dotnet test`. Refuses to run if `CI=true` (so a slipped pipeline env never accepts silently).

CI never sets `SNAPSHOT_ACCEPT`. Mismatches always fail the build in pipelines.

## Why not Verify

Verify is excellent for object-graph diffing, scrubbers, IDE-integrated diff display. It remains the right choice when those features matter. SnapshotAssertions covers the **text-snapshot 80% case** without:

- Verify's `<Deterministic>false</Deterministic>` requirement (which on Linux runners breaks `Microsoft.CodeCoverage`'s instrumentation pipeline; documented at [TUnit#4149](https://github.com/thomhurst/TUnit/discussions/4149))
- The 30-50 lines of per-project file-compare scaffolding consumers otherwise reproduce in every repo

The two libraries can coexist in the same test project; this package does not depend on Verify.

## License

[MIT](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/blob/main/LICENSE) — Copyright (c) 2026 John Verheij
