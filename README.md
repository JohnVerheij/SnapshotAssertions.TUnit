# SnapshotAssertions.TUnit

[![CI](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/actions/workflows/codeql.yml/badge.svg)](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/actions/workflows/codeql.yml)
[![codecov](https://codecov.io/gh/JohnVerheij/SnapshotAssertions.TUnit/branch/main/graph/badge.svg)](https://codecov.io/gh/JohnVerheij/SnapshotAssertions.TUnit)
[![NuGet](https://img.shields.io/nuget/v/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

TUnit-native text-snapshot assertions. AOT-friendly, no reflection, designed for API-surface verification and small text snapshots. Coexists with [Verify](https://github.com/VerifyTests/Verify) (which remains the right choice for object-graph diffing).

> **Status:** pre-release scaffold. The 0.1.0 public surface is in active implementation; CI is wired but the assertion DSL is not yet shipped. See [CHANGELOG.md](CHANGELOG.md) and the [SnapshotAssertions design plan](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/blob/main/docs/design.md) (forthcoming) for the roadmap.

---

## Why this package

For TUnit projects with API-surface snapshot tests (`PublicApiGenerator` → committed `.expected.txt`), the existing options are:

- **Verify (`Verify.TUnit`)** — feature-rich, but its `Verify.props` forces `<Deterministic>false</Deterministic>` (Verify needs absolute PDB paths to find `.verified.txt` files), which on Linux runners breaks `Microsoft.CodeCoverage`'s instrumentation pipeline (produces an empty 178-byte cobertura skeleton). The interaction is documented at [TUnit#4149](https://github.com/thomhurst/TUnit/discussions/4149).
- **Hand-rolled file compare** — `PublicApiGenerator` + `File.ReadAllText` + `string.Equals`. Works, but every project re-invents the same 30-50 lines of accept-flow / file-naming / diff-display scaffolding.

`SnapshotAssertions.TUnit` covers the **text-snapshot 80% case** (string → file comparison) without the coverage friction or the per-project boilerplate. Object-graph diffing, scrubbing, and IDE-integrated diff display are out of scope; use Verify when you need those.

## Install

```
dotnet add package SnapshotAssertions.TUnit
```

`SnapshotAssertions` (the framework-agnostic core) comes transitively. **Requirements:** TUnit 1.43.2 or later, .NET 10. The package is AOT-compatible, trimmable, and uses no reflection.

## Quick start (planned API)

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

That's the entire test. The default file-resolver writes
`Snapshots/{TestClassName}.{TestMethodName}.expected.txt`; on mismatch it writes `*.actual.txt`
next to the expected file and the assertion failure message includes both paths plus a
line-based diff. To accept a change: locally use your IDE's diff-and-merge view, or
`cp Snapshots/X.actual.txt Snapshots/X.expected.txt`. To bulk-accept many at once:
`SNAPSHOT_ACCEPT=1 dotnet test`. CI never sets `SNAPSHOT_ACCEPT`, so mismatches always fail
in pipelines.

## Modern .NET 10+ practices on display

| Practice | Where in this package |
|---|---|
| **AOT-compatible** | `IsAotCompatible=true`. AOT analyzers run during `dotnet build`. No `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` annotations anywhere. |
| **Trimmable** | `IsTrimmable=true`. Tiny public surface; nothing to annotate. |
| **AOT-publish CI gate** | `dotnet publish -r linux-x64 --aot` against the smoke-test consumer. Strongest possible AOT guarantee — not just "AOT-compatible by analyzer," but "actually publishes to native code without warnings." |
| **No reflection, ever** | The package only does file I/O, string comparison, and rendering. `BannedApiAnalyzers` enforces no reflection APIs at build time. |
| **CancellationToken throughout** | Every async public API accepts `CancellationToken ct = default`. |
| **Async file I/O end-to-end** | `File.ReadAllTextAsync`, `File.WriteAllTextAsync`. No sync-over-async. |
| **C# 14 / `LangVersion=14.0`** | File-scoped namespaces, primary constructors, required members, nullable reference types enforced. |
| **`Span<char>` / `ReadOnlySpan<char>` for diff line scanning** | Avoids allocations on hot paths. |
| **Tip-of-tree TFM targeting** | Currently `net10.0`. Per [`CONVENTIONS.md`](CONVENTIONS.md): target current LTS plus current STS during overlap windows; reset to new LTS at SemVer-major version boundaries. |
| **Deterministic builds + Source Link + SBOM + reproducible restore** | Same as siblings. |
| **Trusted Publishing (OIDC) for NuGet** | No long-lived secrets in CI. |
| **5 Roslyn analyzer packs at full strength** | Meziantou, SonarAnalyzer, Roslynator, VSTHRD, dpfa. `TreatWarningsAsErrors=true`. |
| **`PublicApiGenerator` + `SnapshotAssertions` itself for API-surface tests** | Recursive self-test: the package's own API surface is verified using the package. |
| **ApiCompat strict mode (baseline pinned from 0.1.0)** | Prevents silent breaks. |
| **External-consumer smoke test in CI** | Packed `.nupkg`, different namespace, exercises every public entry point, AOT-publishes. |

## Pair with

- **[`LogAssertions.TUnit`](https://www.nuget.org/packages/LogAssertions.TUnit/)** — fluent log assertions over `FakeLogCollector`. Use `MatchesSnapshot()` to pin the rendered output of `LogAssertions`'s `LogAssertionRendering`.

## License

[MIT](LICENSE) — Copyright (c) 2026 John Verheij
