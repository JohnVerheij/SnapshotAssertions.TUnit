# SnapshotAssertions

[![NuGet](https://img.shields.io/nuget/v/SnapshotAssertions.svg)](https://www.nuget.org/packages/SnapshotAssertions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

Framework-agnostic core for text-snapshot assertions.

> **Most users want [`SnapshotAssertions.TUnit`](https://www.nuget.org/packages/SnapshotAssertions.TUnit/), not this package directly.** This is the shared engine; framework-specific adapter packages add the assertion entry points your test framework expects.

---

## What's in this package

- **`SnapshotComparer`** — string-against-file comparison with line-ending, BOM, trailing-whitespace, and trailing-newline options.
- **`SnapshotFileResolver`** — TestContext-aware default file naming (`Snapshots/{TestClass}.{TestMethod}.expected.txt`) and explicit-path resolution.
- **`SnapshotAcceptMode`** — env-var-driven accept logic (`SNAPSHOT_ACCEPT=1` writes the actual content over the expected baseline) with a `CI=true` guard so accidental pipeline acceptance is impossible.
- **`SnapshotOptions`** — line-ending, BOM, whitespace, and trailing-newline configuration with strict defaults.
- **`LineDiffRenderer`** — terminal line-based diff display, truncated to the first 20 differing lines for very large diffs (`LineDiffRenderer.MaxDifferingLines = 20`).
- **`SnapshotScrubber` + `Scrubbers` factory** *(v0.2.0+)* — text-transform pipeline that replaces volatile substrings (GUIDs, ISO 8601 timestamps, Unix-epoch-millis numbers, custom regex matches) with stable indexed tokens before comparison. Five built-ins (`Scrubbers.Guid`, `Scrubbers.Iso8601Timestamp`, `Scrubbers.UnixEpochMillis`, two `Scrubbers.Pattern` overloads), one curated chain (`Scrubbers.Default`), and `SnapshotScrubberState` for stable indexed-token assignment across recurring values.

## Test-framework adapters

| Package | Test framework | Status |
|---|---|---|
| [`SnapshotAssertions.TUnit`](https://www.nuget.org/packages/SnapshotAssertions.TUnit/) | TUnit | Available now |
| `SnapshotAssertions.NUnit` | NUnit | Possible if there is demand |
| `SnapshotAssertions.xUnit` | xUnit | Possible if there is demand |
| `SnapshotAssertions.MSTest` | MSTest | Possible if there is demand |

If you'd find a non-TUnit adapter useful, [open a feature request](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/issues/new?template=feature_request.yml) — adapters are not built proactively.

## Installation

```
dotnet add package SnapshotAssertions.TUnit
```

`SnapshotAssertions` comes transitively. You don't need to install it directly unless you're building your own adapter package.

## Stability

The public surfaces above are semver-bound. Breaking changes require a major version bump. The exact text format of line-based diff output is **not stable** and may gain extra detail or change formatting in any release.

## License

[MIT](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/blob/main/LICENSE) — Copyright (c) 2026 John Verheij
