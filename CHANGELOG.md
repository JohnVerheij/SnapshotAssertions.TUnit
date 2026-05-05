# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] — Initial release: text-snapshot assertions for TUnit

First public release. Two packages ship in lockstep: `SnapshotAssertions` (framework-agnostic
core) and `SnapshotAssertions.TUnit` (TUnit adapter). Net 10, AOT-compatible, trimmable, no
runtime reflection.

### Added (SnapshotAssertions, framework-agnostic core)

- **`SnapshotComparer`** — pure string-against-string comparison with option-driven
  normalization (line endings, BOM, trailing whitespace, trailing newline). Stateless;
  callable from any test framework.
- **`SnapshotOptions`** (sealed record) plus four enum types (`SnapshotLineEndingMode`,
  `SnapshotBomHandling`, `SnapshotTrailingWhitespace`, `SnapshotTrailingNewline`) for
  configuring the comparison. Strict-by-default (`SnapshotOptions.Default`); cross-platform
  preset available via `SnapshotOptions.NormalizedLineEndings`.
- **`SnapshotEvaluator`** — orchestrates a single comparison: reads the expected baseline,
  invokes the comparer, applies accept-mode when active, writes the actual file on
  mismatch / no-baseline, and returns a `SnapshotResult`.
- **`SnapshotFileResolver`** — pure path construction: `ResolveByName`, `ResolveByTest`,
  `ResolveByFile`, plus `GetDefaultSnapshotsDirectory` for the conventional `Snapshots/`
  folder under a base directory.
- **`SnapshotAcceptMode`** — env-var-driven accept logic: active when `SNAPSHOT_ACCEPT`
  is truthy AND `CI` is not. Pure `IsActive(snapshotAcceptValue, ciValue)` overload for
  deterministic testing without touching the host environment.
- **`LineDiffRenderer`** — line-by-line diff with unified-diff-style prefixes
  (` `/`-`/`+`); truncated to 20 differing lines for very large snapshots.
- **`SnapshotResult`** + **`SnapshotMatchOutcome`** (`Matched`, `Mismatched`, `NoBaseline`,
  `Accepted`) plus `SnapshotPaths` record-struct.
- **`SnapshotException`** carrying the `SnapshotResult` for programmatic access; its message
  is the same human-readable form rendered by `SnapshotResult.Describe()`.

### Added (SnapshotAssertions.TUnit, TUnit adapter)

- **`SnapshotAssertion`** — TUnit `Assertion<string>` with `[AssertionExtension("MatchesSnapshot")]`
  generating the entry method `Assert.That(actualText).MatchesSnapshot()`. Chain methods:
  - `.WithName(string)` — overrides the default test-context-derived snapshot name
  - `.AtPath(string)` — overrides path resolution with an explicit absolute / relative path
  - `.WithOptions(SnapshotOptions)` — overrides comparison options
- **Shorthand entry-point extensions** in `TUnit.Assertions.Extensions` (auto-imports along
  the same path as the source-generated entry):
  - `MatchesSnapshot(string snapshotName)`
  - `MatchesSnapshot(SnapshotOptions options)`
  - `MatchesSnapshot(string snapshotName, SnapshotOptions options)`
  - `MatchesSnapshotFile(string filePath)`
  - `MatchesSnapshotFile(string filePath, SnapshotOptions options)`

### Zero-config project setup

The TUnit-adapter package ships a `build/SnapshotAssertions.TUnit.targets` file inside the
`.nupkg`. NuGet auto-imports it into the consuming project, which auto-includes
`Snapshots/**/*.expected.txt` with `CopyToOutputDirectory="PreserveNewest"`. Consumers do not
write any csproj wiring — install the package and start writing tests. To opt out for custom
snapshot folder layouts, set
`<SnapshotAssertionsAutoIncludeSnapshots>false</SnapshotAssertionsAutoIncludeSnapshots>` in
the test project.

### Default file resolution

When `MatchesSnapshot()` is called without `.WithName` / `.AtPath` chains, the assertion
reads `TestContext.Current.Metadata.TestDetails` to build the path
`{AppContext.BaseDirectory}/Snapshots/{TestClassName}.{TestMethodName}.expected.txt`.
On mismatch or missing baseline, the actual content is written to a sibling `.actual.txt`
file. Throws `InvalidOperationException` with a clear diagnostic when called outside a TUnit
test context (no `TestContext.Current`).

### Accept-changes workflow

Two paths to accept a baseline change:

- Per-snapshot, manual: copy the `.actual.txt` over the `.expected.txt`.
- Bulk: set `SNAPSHOT_ACCEPT=1` in your local shell and run `dotnet test`. The CI guard
  refuses accept-mode if `CI=true` is also set, so a stray pipeline configuration cannot
  silently accept baseline drift.

### Quality bar

- AOT-compatible (`IsAotCompatible=true`), trimmable (`IsTrimmable=true`), no runtime
  reflection in the assertion path.
- C# 14, `Nullable=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`.
- 5 Roslyn analyzer packs at full strength (Meziantou, SonarAnalyzer, Roslynator, VSTHRD, dpfa).
- ApiCompat strict mode wired (`PackageValidationBaselineVersion` will pin to 0.1.0 in 0.1.1).
- 90% line / 80% branch coverage CI gates.
- AOT-publish CI gate against the external-consumer smoke test.
- Trusted Publishing (OIDC) to nuget.org — no long-lived secrets.
- Source Link, SBOM via `Microsoft.Sbom.Targets`, deterministic builds, lock files,
  `--locked-mode` restore on CI.
- 51 tests passing across the core and adapter test projects.

### Deferred to follow-up releases

- **Recursive public-API self-test** (`SnapshotAssertions.TUnit.SnapshotTests` project) — uses
  `MatchesSnapshot()` against `PublicApiGenerator` output to pin the public surface across
  releases. Bootstrapping the initial committed `.expected.txt` baseline needs a one-time
  local accept run; deferred to 0.1.1 once that flow is fully documented.
- JSON-aware snapshot comparison (`MatchesJsonSnapshot()`) — planned for 0.2.0.
- Pattern-based scrubbing (`MatchesSnapshotScrubbed(IScrubber)`) — planned for 0.3.0.
