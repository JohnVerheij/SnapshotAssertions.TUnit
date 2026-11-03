# SnapshotAssertions

[![NuGet](https://img.shields.io/nuget/v/SnapshotAssertions.svg)](https://www.nuget.org/packages/SnapshotAssertions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

> Part of the **[DotNetAssertions](https://dotnetassertions.dev)** family. This is the framework-agnostic core; the TUnit assertions live in the matching `.TUnit` package.

Framework-agnostic core for text-snapshot assertions.

> **For TUnit test projects, install [`SnapshotAssertions.TUnit`](https://www.nuget.org/packages/SnapshotAssertions.TUnit/) instead; this package comes with it transitively.** `SnapshotAssertions` is the framework-agnostic shared engine; framework-specific adapter packages add the assertion entry points each test framework expects.

---

## What's in this package

- **`SnapshotComparer`**: string-against-string comparison with line-ending, BOM, trailing-whitespace, and trailing-newline options.
- **`SnapshotEvaluator`**: reads the baseline file, compares, and writes the actual file (or, in accept mode, the baseline).
- **`SnapshotFileResolver`**: default file naming from the test class and method names (`Snapshots/{TestClass}.{TestMethod}.expected.txt`) and explicit-path resolution. A baseline is the plain expected text with no header or metadata, so an explicit path can point at an existing golden file with any name and extension.
- **`SnapshotAcceptMode`**: decides whether accept mode is on: `SNAPSHOT_ACCEPT` is `1`, `true` or `yes`, and the process is not in CI (the guard against accepting in a pipeline): `CI` or `TF_BUILD` (Azure Pipelines) set to a truthy value counts as CI, unless `SNAPSHOT_ACCEPT_IN_CI` is truthy.
- **`SnapshotOptions`**: line-ending, BOM, whitespace, and trailing-newline configuration with strict defaults, plus a caller-supplied `WithNormalizer(Func<string, string>)` pre-comparison transform *(v0.5.0+)*.
- **`LineDiffRenderer`**: terminal line-based diff display, truncated to the first 20 differing lines for very large diffs (`LineDiffRenderer.MaxDifferingLines = 20`).
- **`SnapshotScrubber` + `Scrubbers` factory** *(v0.2.0+)*: text-transform pipeline that replaces volatile substrings (GUIDs, ISO 8601 timestamps, Unix-epoch-millis numbers, custom regex matches) with stable indexed tokens before comparison. Built-ins (`Scrubbers.Guid`, `Scrubbers.Iso8601Timestamp`, `Scrubbers.UnixEpochMillis`, plus `Scrubbers.GuidN` and `Scrubbers.ElapsedMs` *(v0.4.0+)*), curated chains (`Scrubbers.Default` for the v0.2.0 three-pattern set, `Scrubbers.Common` *(v0.4.0+)* for the extended five-pattern set), two `Scrubbers.Pattern` overloads for custom regex (flat literal token) plus two `Scrubbers.IndexedPattern` overloads *(v0.5.0+)* for the indexed `<kind:N>` counterpart that keeps recurring matched values correlated, and `SnapshotScrubberState` for stable indexed-token assignment across recurring values. `Scrubbers.Combine(params SnapshotScrubber[])` *(v0.3.0+)* assembles an array of scrubbers into a single composite for reusable bundles.
- **`DiffSuggestion` + `DiffSuggestionAnalyzer`** *(v0.4.0+)*: scans a snapshot-mismatch diff for known volatile patterns and surfaces scrubber recommendations. Integrated into `SnapshotResult.Describe()` so failure messages include a top-3-capped "Suggestion(s)" section pointing at applicable built-ins.
- **`SnapshotAssertions.Render.SnapshotRenderer<T>` + `Renderer.For<T>`** *(v0.4.0+)*: abstract base class and inline factory for projecting domain types to canonical strings before snapshot comparison. Adapter packages consume these via their own `MatchesSnapshot<T>` overloads.

## Test-framework adapters

| Package | Test framework | Status |
|---|---|---|
| [`SnapshotAssertions.TUnit`](https://www.nuget.org/packages/SnapshotAssertions.TUnit/) | TUnit | Available now |

Adapters for other test frameworks (NUnit, xUnit, MSTest) are not planned; the family targets TUnit.

## Installation

```
dotnet add package SnapshotAssertions.TUnit
```

`SnapshotAssertions` comes transitively. You don't need to install it directly unless you're building your own adapter package.

## Stability

Before 1.0, a minor version can contain breaking changes, each marked **BREAKING** in the CHANGELOG; renaming or removing one of the intended-stable types listed in the [full README](https://github.com/JohnVerheij/SnapshotAssertions.TUnit#stability-intent-pre-10) would need a major version even before 1.0. The exact text format of line-based diff output is **not stable** and may gain extra detail or change formatting in any release.

## License

[MIT](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/blob/main/LICENSE). Copyright (c) 2026 John Verheij.
