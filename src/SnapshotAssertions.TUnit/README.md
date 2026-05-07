# SnapshotAssertions.TUnit

[![NuGet](https://img.shields.io/nuget/v/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![Downloads](https://img.shields.io/nuget/dt/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

TUnit-native text-snapshot assertions on top of TUnit's `[AssertionExtension]` source generator. AOT-compatible, trimmable, no reflection. Coexists with [Verify](https://github.com/VerifyTests/Verify); does not replace it for object-graph cases.

> **Full documentation, full options reference, design notes, and roadmap:** [github.com/JohnVerheij/SnapshotAssertions.TUnit](https://github.com/JohnVerheij/SnapshotAssertions.TUnit)

## Install

```
dotnet add package SnapshotAssertions.TUnit
```

`SnapshotAssertions` (the framework-agnostic core) comes transitively. **Requirements:** TUnit 1.43.11 or later, .NET 10.

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

## Scrubbers (v0.2.0+)

For snapshots that contain volatile values (GUIDs, ISO 8601 timestamps, Unix-epoch-millis numbers, request IDs, etc.), chain `.WithScrubber(...)` calls to replace them with stable indexed tokens before comparison. Recurring values share an index; different kinds maintain independent counters.

```csharp
using SnapshotAssertions;

// Curated default: Guid + Iso8601Timestamp + UnixEpochMillis
await Assert.That(jsonResponse)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Default);

// Custom regex: replace request-id headers with a literal token
await Assert.That(httpLog)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Pattern(@"\brequest-id=[a-f0-9-]+", "request-id=<scrubbed>"));
```

The built-in indexed scrubbers (`Scrubbers.Guid`, `Scrubbers.Iso8601Timestamp`, `Scrubbers.UnixEpochMillis`) emit `<kind:N>` tokens where N is assigned by first-occurrence order per kind. The same value at every site keeps the same N. `Scrubbers.Pattern(...)` overloads emit a literal token (no indexing).

[Full Scrubbers reference, custom-scrubber recipe, and design notes on GitHub.](https://github.com/JohnVerheij/SnapshotAssertions.TUnit#scrubbers-volatile-value-handling)

## Why not Verify

Verify is excellent for object-graph diffing, scrubbers, IDE-integrated diff display. It remains the right choice when those features matter. SnapshotAssertions covers the **text-snapshot 80% case** without:

- Verify's `<Deterministic>false</Deterministic>` requirement (which on Linux runners breaks `Microsoft.CodeCoverage`'s instrumentation pipeline; documented at [TUnit#4149](https://github.com/thomhurst/TUnit/discussions/4149))
- The 30-50 lines of per-project file-compare scaffolding consumers otherwise reproduce in every repo

The two libraries can coexist in the same test project; this package does not depend on Verify.

## License

[MIT](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/blob/main/LICENSE) — Copyright (c) 2026 John Verheij
