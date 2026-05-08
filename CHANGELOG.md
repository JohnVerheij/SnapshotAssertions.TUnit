# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0]: Built-in scrubbers, dependency refresh

Feature release plus rolled-in housekeeping. Lockstep version bump for both packages; ApiCompat baseline pinned to 0.1.0 (the previous shipped release). The intermediate v0.1.1 housekeeping work is folded into this release rather than shipping as a separate intermediate version.

### Added (SnapshotAssertions, framework-agnostic core)

- **`SnapshotScrubber` abstract base + `Scrubbers` static factory** for transforming snapshot text before comparison. Replaces volatile substrings (GUIDs, timestamps, epoch millis) with stable indexed tokens so a baseline survives multiple test runs.
- **Five built-in scrubbers:**
  - `Scrubbers.Guid`: replaces 8-4-4-4-12 hex GUIDs (case-insensitive) with `<guid:N>`.
  - `Scrubbers.Iso8601Timestamp`: replaces ISO 8601 timestamps (with optional fractional seconds and Z / `±HH:MM` zone) with `<iso8601:N>`.
  - `Scrubbers.UnixEpochMillis`: replaces 13-digit Unix-millis numbers (at word boundaries) with `<unixms:N>`.
  - `Scrubbers.Pattern(Regex, string)` and `Scrubbers.Pattern(string, string)`: replaces every regex match with the literal token. The string overload compiles with `RegexOptions.NonBacktracking | RegexOptions.CultureInvariant` (ReDoS-resistant). No indexing.
- **`Scrubbers.Default`**: curated chain of `Guid + Iso8601Timestamp + UnixEpochMillis` for the common case.
- **Indexed-token format (`<kind:N>`)**: recurring volatile values share an index (first-occurrence order, per kind). Different kinds maintain independent index counters. State is per-snapshot evaluation; never crosses test boundaries.
- **`SnapshotScrubberState`**: public per-snapshot state object (a kind / value → index map) used by indexed scrubbers. Consumers extending `SnapshotScrubber` with custom indexed scrubbers reuse the same state via `state.GetOrAssignIndex(kind, value)`.

### Added (SnapshotAssertions.TUnit, TUnit adapter)

- **`MatchesSnapshot(...).WithScrubber(SnapshotScrubber)` chain method.** Multiple `WithScrubber` calls compose left-to-right: the first scrubber receives the raw actual content; each subsequent scrubber receives the previous scrubber's output. All scrubbers in the chain share a single `SnapshotScrubberState` so recurring volatile values keep stable tokens across the whole snapshot.

### Changed

- **Dependency refresh.** Bumped to latest stable for every direct and analyzer dependency:
  - `TUnit` / `TUnit.Assertions` / `TUnit.Core`: 1.43.2 → 1.43.11
  - `PublicApiGenerator`: 11.4.6 → 11.5.4
  - `Microsoft.Sbom.Targets`: 3.0.1 → 4.1.5
  - `Microsoft.SourceLink.GitHub`: 8.0.0 → 10.0.203
  - `DotNetProjectFile.Analyzers`: 1.12.2 → 1.13.1
  - `Meziantou.Analyzer`: 2.0.219 → 3.0.72
  - `Microsoft.VisualStudio.Threading.Analyzers`: 17.13.61 → 17.14.15
  - `Roslynator.Analyzers`: 4.13.1 → 4.15.0
  - `SonarAnalyzer.CSharp`: 10.24.0.138807 → 10.25.0.139117
- **CI branch-coverage gate raised from 80% → 90%.** The line-coverage gate stays at 90%. Current branch coverage is comfortably above 90% so the threshold is tightened to keep regressions visible.

### Added (CI / process)

- **Recursive public-API self-test project** (`tests/SnapshotAssertions.TUnit.SnapshotTests/`): pins the public surface using THIS package's own `MatchesSnapshot()` chain against `PublicApiGenerator` output. Pure dogfooding: the snapshot tool tests itself on its own public surface. Originally deferred to v0.1.1; folded into v0.2.0.

### Quality numbers

- Coverage on the main suite: **98.52% line / 92.25% branch** (above the CI hard gates of 90% / 90%).
- ApiCompat strict-mode validation against the v0.1.0 baseline (`PackageValidationBaselineVersion=0.1.0`); auto-generated `CompatibilitySuppressions.xml` documents every additive change.

### Documentation

- **`CONVENTIONS.md` upgraded to v0.2.** Codifies the family-wide conventions shared across `SnapshotAssertions.TUnit`, `LogAssertions.TUnit`, and `TimeAssertions.TUnit`: trailing `CancellationToken ct = default` on every new async API, `Task.Delay(TimeSpan, TimeProvider, ct)` for polling loops, the 100/200/400/800/1000ms exponential schedule for time-based polls, the `# <Package> snapshot v<N>` header convention for `ToSnapshotString()`, TFM policy (LTS-anchored; multi-target during STS support windows), and the explicit "Verify is not promoted by this family: `MatchesSnapshot()` is the canonical example" stance.

## [0.1.0]: Initial release: text-snapshot assertions for TUnit

First public release. Two packages ship in lockstep: `SnapshotAssertions` (framework-agnostic
core) and `SnapshotAssertions.TUnit` (TUnit adapter). Net 10, AOT-compatible, trimmable, no
runtime reflection.

### Added (SnapshotAssertions, framework-agnostic core)

- **`SnapshotComparer`**: pure string-against-string comparison with option-driven
  normalization (line endings, BOM, trailing whitespace, trailing newline). Stateless;
  callable from any test framework.
- **`SnapshotOptions`** (sealed record) plus four enum types (`SnapshotLineEndingMode`,
  `SnapshotBomHandling`, `SnapshotTrailingWhitespace`, `SnapshotTrailingNewline`) for
  configuring the comparison. Strict-by-default (`SnapshotOptions.Default`); cross-platform
  preset available via `SnapshotOptions.NormalizedLineEndings`.
- **`SnapshotEvaluator`**: orchestrates a single comparison: reads the expected baseline,
  invokes the comparer, applies accept-mode when active, writes the actual file on
  mismatch / no-baseline, and returns a `SnapshotResult`.
- **`SnapshotFileResolver`**: pure path construction: `ResolveByName`, `ResolveByTest`,
  `ResolveByFile`, plus `GetDefaultSnapshotsDirectory` for the conventional `Snapshots/`
  folder under a base directory.
- **`SnapshotAcceptMode`**: env-var-driven accept logic: active when `SNAPSHOT_ACCEPT`
  is truthy AND `CI` is not. Pure `IsActive(snapshotAcceptValue, ciValue)` overload for
  deterministic testing without touching the host environment.
- **`LineDiffRenderer`**: line-by-line diff with unified-diff-style prefixes
  (` `/`-`/`+`); truncated to 20 differing lines for very large snapshots.
- **`SnapshotResult`** + **`SnapshotMatchOutcome`** (`Matched`, `Mismatched`, `NoBaseline`,
  `Accepted`) plus `SnapshotPaths` record-struct.
- **`SnapshotException`** carrying the `SnapshotResult` for programmatic access; its message
  is the same human-readable form rendered by `SnapshotResult.Describe()`.

### Added (SnapshotAssertions.TUnit, TUnit adapter)

- **`SnapshotAssertion`**: TUnit `Assertion<string>` with `[AssertionExtension("MatchesSnapshot")]`
  generating the entry method `Assert.That(actualText).MatchesSnapshot()`. Chain methods:
  - `.WithName(string)`: overrides the default test-context-derived snapshot name
  - `.AtPath(string)`: overrides path resolution with an explicit absolute / relative path
  - `.WithOptions(SnapshotOptions)`: overrides comparison options
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
write any csproj wiring: install the package and start writing tests. To opt out for custom
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
- Trusted Publishing (OIDC) to nuget.org: no long-lived secrets.
- Source Link, SBOM via `Microsoft.Sbom.Targets`, deterministic builds, lock files,
  `--locked-mode` restore on CI.
- 51 tests passing across the core and adapter test projects.

### Deferred to follow-up releases

- **Recursive public-API self-test** (`SnapshotAssertions.TUnit.SnapshotTests` project): uses
  `MatchesSnapshot()` against `PublicApiGenerator` output to pin the public surface across
  releases. Bootstrapping the initial committed `.expected.txt` baseline needs a one-time
  local accept run; deferred to 0.1.1 once that flow is fully documented. *(Resolved: shipped in [0.2.0](#020--built-in-scrubbers-dependency-refresh).)*
- JSON-aware snapshot comparison (`MatchesJsonSnapshot()`): planned for 0.2.0.
- Pattern-based scrubbing (`MatchesSnapshotScrubbed(IScrubber)`): planned for 0.3.0. *(Resolved earlier: shipped as `WithScrubber()` in [0.2.0](#020--built-in-scrubbers-dependency-refresh).)*
