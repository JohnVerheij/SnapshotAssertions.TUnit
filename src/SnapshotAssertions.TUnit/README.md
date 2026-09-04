# SnapshotAssertions.TUnit

[![NuGet](https://img.shields.io/nuget/v/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![Downloads](https://img.shields.io/nuget/dt/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

> Part of the **[DotNetAssertions](https://dotnetassertions.dev)** family of assertion extensions for TUnit.

TUnit-native text-snapshot assertions on top of TUnit's `[AssertionExtension]` source generator. AOT-compatible, trimmable, no reflection. Coexists with [Verify](https://github.com/VerifyTests/Verify); does not replace it for object-graph cases.

> **Full documentation, full options reference, design notes, and roadmap:** [github.com/JohnVerheij/SnapshotAssertions.TUnit](https://github.com/JohnVerheij/SnapshotAssertions.TUnit)

## Install

```
dotnet add package SnapshotAssertions.TUnit
```

`SnapshotAssertions` (the framework-agnostic core) comes transitively. **Requirements:** TUnit 1.66.8 or later, .NET 10.

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

As of v0.6.0, bulk accept writes the new baseline straight into the **source** `Snapshots/` folder (resolved via `SnapshotFileResolver.TryResolveSourceSnapshotsDirectory`), so the committed baseline updates in place even under `dotnet test --no-build`. It falls back to the test binary's directory only when the source path cannot be resolved.

CI never sets `SNAPSHOT_ACCEPT`. Mismatches always fail the build in pipelines.

## Scrubbers

For snapshots that contain volatile values (GUIDs, ISO 8601 timestamps, Unix-epoch-millis numbers, request IDs, etc.), chain `.WithScrubber(...)` calls to replace them with stable indexed tokens before comparison. Recurring values share an index; different kinds maintain independent counters.

```csharp
using SnapshotAssertions;

// Curated default: Guid + Iso8601Timestamp + UnixEpochMillis
await Assert.That(jsonResponse)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Default);

// Extended curated chain (v0.4.0+): adds GuidN + ElapsedMs to the Default set
await Assert.That(diagnostic)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Common);

// Custom regex: replace request-id headers with a literal token
await Assert.That(httpLog)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Pattern(@"\brequest-id=[a-f0-9-]+", "request-id=<scrubbed>"));

// Custom regex with correlation kept (v0.5.0+): recurring values share <kind:N>
await Assert.That(text)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.IndexedPattern(@"\bticket-\d+\b", "ticket"));

// Assemble a reusable bundle once; pass as a single scrubber (v0.3.0+)
private static readonly SnapshotScrubber FixturesScrubber = Scrubbers.Combine(
    Scrubbers.Common,
    Scrubbers.Pattern(@"\brequest-id=[a-f0-9-]+", "request-id=<scrubbed>"));
```

The built-in indexed scrubbers emit `<kind:N>` tokens where N is assigned by first-occurrence order per kind. The same value at every site keeps the same N. `Scrubbers.Pattern(...)` overloads emit a literal token (no indexing); `Scrubbers.IndexedPattern(...)` (v0.5.0+) is the indexed counterpart for custom regex, so recurring matched values keep a shared `<kind:N>` index. `Scrubbers.Combine(...)` (v0.3.0+) wraps an array of scrubbers into a single composite so a reused bundle does not have to be re-chained on every assertion. `Scrubbers.Common` (v0.4.0+) is the extended curated chain: `Guid` + `GuidN` + `Iso8601Timestamp` + `UnixEpochMillis` + `ElapsedMs`. Reach for `Common` first; fall back to `Default` for the v0.3.0-and-earlier three-pattern chain only when an existing baseline depends on it.

[Full Scrubbers reference, custom-scrubber recipe, and design notes on GitHub.](https://github.com/JohnVerheij/SnapshotAssertions.TUnit#scrubbers-volatile-value-handling)

## Smart-diff suggestions in failure messages

On a snapshot mismatch, the failure message now scans the rendered diff for known volatile patterns and recommends applicable built-in scrubbers automatically. No configuration is required. Wider diffs that match many patterns get a top-3 list plus a `... and N more` rollup, so the failure message stays scannable.

[Smart-diff suggestions reference on GitHub.](https://github.com/JohnVerheij/SnapshotAssertions.TUnit#cookbook-common-patterns)

## Renderer pattern for typed values

For values that are not already strings, project them via a renderer:

```csharp
// Inline delegate projection.
await Assert.That(myProto)
    .MatchesSnapshot(p => Formatter.Format(p))
    .WithScrubber(Scrubbers.Common);

// Reusable subclass for project-wide canonical renderers.
internal sealed class MyProtoRenderer : SnapshotRenderer<MyProto>
{
    public override string Render(MyProto value) => Formatter.Format(value);
}
// ...
await Assert.That(myProto).MatchesSnapshot(new MyProtoRenderer());
```

The two overloads enable sibling family packages (`LogAssertions.TUnit`, `MathAssertions.TUnit`, etc.) to publish renderers for their own types as static helper methods without taking a reference on `SnapshotAssertions`. Consumers compose at the test call site via the delegate overload.

[Renderer pattern reference and sibling-family composition recipe on GitHub.](https://github.com/JohnVerheij/SnapshotAssertions.TUnit#cookbook-common-patterns)

## Parameterized tests (`[Arguments]`)

For parameterized tests, the default file resolver hashes the row's argument values and appends an 8-hex-character suffix to the snapshot file name, so each row gets a distinct baseline:

```csharp
[Test]
[Arguments("alpha", 200)]
[Arguments("beta", 404)]
public async Task Response_per_route_matches(string route, int statusCode)
{
    await Assert.That(RenderResponse(route, statusCode)).MatchesSnapshot();
}
```

Baselines land at `Snapshots/{TestClassName}.{TestMethodName}.{ArgsHash8}.expected.txt`. The hash is `InvariantCulture`-stable so the same arguments produce the same file across developer machines and CI. Collection arguments are expanded element-by-element (since v0.7.0), so rows that differ only inside an array get distinct files. [Full details on GitHub.](https://github.com/JohnVerheij/SnapshotAssertions.TUnit#cookbook-common-patterns)

## Why not Verify

Verify is excellent for object-graph diffing, scrubbers, IDE-integrated diff display. It remains the right choice when those features matter. SnapshotAssertions covers the **text-snapshot 80% case** without:

- Verify's `<Deterministic>false</Deterministic>` requirement (which on Linux runners breaks `Microsoft.CodeCoverage`'s instrumentation pipeline; documented at [TUnit#4149](https://github.com/thomhurst/TUnit/discussions/4149))
- The 30-50 lines of per-project file-compare scaffolding consumers otherwise reproduce in every repo

The two libraries can coexist in the same test project; this package does not depend on Verify.

## Family

Part of an assertion family for TUnit:

- [LogAssertions.TUnit](https://github.com/JohnVerheij/LogAssertions.TUnit)
- [TimeAssertions.TUnit](https://github.com/JohnVerheij/TimeAssertions.TUnit)
- [MathAssertions.TUnit](https://github.com/JohnVerheij/MathAssertions.TUnit)
- [JsonAssertions.TUnit](https://github.com/JohnVerheij/JsonAssertions.TUnit)
- [SseAssertions.TUnit](https://github.com/JohnVerheij/SseAssertions.TUnit)
- [GrpcAssertions.TUnit](https://github.com/JohnVerheij/GrpcAssertions.TUnit)

## License

[MIT](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/blob/main/LICENSE). Copyright (c) 2026 John Verheij.
