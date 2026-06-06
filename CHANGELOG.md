# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.6.1] - 2026-06-05: document source-tree accept under --no-build

Documentation and release-tooling patch. No code, public API, or behavior change; the `0.6.0` ApiCompat baseline surface is unchanged.

### Changed

- README accept-changes workflow corrected for the `0.6.0` behavior: `SNAPSHOT_ACCEPT=1` writes the new baseline directly into the **source** `Snapshots/` folder (resolved via `SnapshotFileResolver.TryResolveSourceSnapshotsDirectory`), so the committed baseline updates in place even under `dotnet test --no-build`, where the build does not copy snapshot files back to source. The prior text still described the pre-`0.6.0` "manually move the bin-directory files into source" step; it now documents the source-tree write with the bin-directory write called out only as the fallback when the source path cannot be resolved. The packed package README gains the same note.
- Bumped `PackageValidationBaselineVersion` from `0.5.0` to `0.6.0` on both packages so ApiCompat strict-mode validates `0.6.1` against the most recently published baseline. Documentation-only; no `CompatibilitySuppressions.xml` change.
- The release workflow now publishes the matching `CHANGELOG.md` section as the GitHub release body (`body_path`), so release notes carry the full hand-written detail instead of GitHub's auto-generated commit summary.

## [0.6.0] - 2026-06-04: Source-tree accept resolution and baseline-generation fixes

Minor release. Adds a public source-tree resolution helper used by accept-mode, plus two behavior fixes to the baseline-generation path. Both fixes correct cases where a first-run or accepted baseline ended up in a form the next comparison would not read back as written.

### Added

- **`SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(string startDirectory)`** walks up from a runtime directory (typically `AppContext.BaseDirectory`) to the nearest ancestor project directory and returns its `Snapshots/` folder, or `null` when no source tree is present. Accept-mode uses it to write an accepted baseline into the committable source tree rather than the runtime copy under `bin/`. Additive; validated against the `0.5.0` ApiCompat baseline.

### Fixed

- **`SnapshotOptions.WithNormalizer` baseline candidate** is now written in normalized form. Previously, on a first run with no baseline (and on accept-mode), the persisted candidate was the raw, un-normalized subject, so a consumer who accepted it committed un-canonicalized or unscrubbed volatile text that the very next comparison normalized away. The candidate (the `.actual.txt` a consumer renames to accept, and the `.expected.txt` accept-mode overwrites) now carries the same option-driven normalized form the comparison reads back, matching how Verify scrubs before writing its `.received` file.
- **`SNAPSHOT_ACCEPT=1` accept target** now resolves to the source-tree `Snapshots/` directory rather than the runtime copy under `bin/`. Previously, under `dotnet test --no-build`, accepting wrote the `.expected.txt` next to the runtime location, so it never reached the committable `tests/.../Snapshots/` source folder and CI later reported the baseline as missing. Accept-mode now walks up from `AppContext.BaseDirectory` to the nearest ancestor project directory (the folder the build's include glob is relative to) and writes there, so accepting lands the baseline where it is committed and read from. The new `SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(string)` helper performs this resolution and falls back to the runtime directory when no source tree is present. The read path is unchanged; only the accept write target moves.

## [0.5.0] - 2026-06-03: SnapshotOptions.WithNormalizer

Feature release. Adds `SnapshotOptions.WithNormalizer(Func<string, string>)`, a caller-supplied transform applied to both the actual content and the expected baseline before any built-in normalization. It generalizes the built-in line-ending normalizer into an arbitrary pre-comparison transform: canonicalize JSON or XML, sort nondeterministic collections, mask volatile fields, or reformat numbers before the snapshot compares. Composing two of these (a canonicalizer in a sibling package plus this normalizer here) is how the family does JSON snapshots without either package depending on the other. Also folds in the accumulated CI hardening, the Renovate migration, and the CONVENTIONS v0.7 sync from the unreleased line.

### Added

- **`SnapshotOptions.WithNormalizer(Func<string, string> normalizer)`** returns a copy of the options with a transform applied to both sides before BOM, line-ending, trailing-whitespace, and trailing-newline handling. Chaining composes in registration order (the first registered runs first). The transform sees the raw rendered text, so it is the natural seam for canonicalizing content whose textual form is noisy but semantically irrelevant. The backing **`SnapshotOptions.Normalizer`** property (a `Func<string, string>?`, default `null`) is also public for direct construction. A normalizer that returns `null` fails with an `InvalidOperationException` rather than a downstream `NullReferenceException`.
- **`Scrubbers.IndexedPattern(Regex pattern, string kind)`** replaces every match with `<kind:N>`, where recurring identical matched values share the same index N and distinct values get incrementing indices in first-occurrence order. Correlation is ordinal on the matched value. It is the indexed counterpart to `Scrubbers.Pattern`: where `Pattern` is flat (every match collapses to one literal token, losing correlation), `IndexedPattern` reuses the same `SnapshotScrubberState.GetOrAssignIndex` machinery as the built-in indexed scrubbers, so behavior matches the built-ins exactly. Passing a built-in `kind` (e.g. `"guid"`) shares that built-in's index counter. This closes the flat-vs-indexed asymmetry: the built-ins were indexed but the custom-regex path was flat-only, leaving a consumer with a volatile value outside the built-in kinds unable to keep same-value correlation. No breaking changes.
- **`Scrubbers.IndexedPattern(string pattern, string kind)`** compiles `pattern` with `RegexOptions.NonBacktracking | RegexOptions.CultureInvariant` and otherwise behaves identically to the `Regex` overload, mirroring the existing `Scrubbers.Pattern(string, string)` ergonomics.

### Changed

- Added a README cookbook recipe, "Snapshotting serialized/binary formats via a canonical renderer": render the decoded wire bytes canonically (one field per line) through the existing `SnapshotRenderer<T>` path rather than snapshotting `ToString()` / JSON, so the assertion is a wire test rather than a content test. The recipe stays format-agnostic (protobuf / XML / MessagePack). The `Scrubbers.Pattern` section now documents `IndexedPattern` alongside the flat `Pattern` overloads.
- Removed `paths-ignore` from `.github/workflows/ci.yml` so the `Build, test & pack` required check always reports a status. Without the fix, docs-only PRs stuck in `Expected - Waiting for status to be reported` and could not satisfy branch protection.
- Dropped drift-prone own-version anchors from the packed adapter README's section headings: `## Scrubbers (v0.2.0+)` is now `## Scrubbers`; `## Smart-diff suggestions in failure messages (v0.4.0+)` is now `## Smart-diff suggestions in failure messages`; `## Renderer pattern for typed values (v0.4.0+)` is now `## Renderer pattern for typed values`. Historical "added in vX.Y" markers in body prose (e.g. `(v0.4.0+)` annotations next to specific scrubber names) are unchanged. The CHANGELOG remains the single source of truth for what shipped when.
- Migrated CI dependency automation from Dependabot to Renovate (`.github/renovate.json`), matching `SseAssertions.TUnit` and `TimeAssertions.TUnit`. Daily schedule (before 4am Europe/Amsterdam), `customManagers` keep TUnit version literals in the root README, package README, smoketest csproj, and bug-report Issue Form in lockstep with the central `Directory.Packages.props` pin. `platformAutomerge` replaces the separate `dependabot-auto-merge.yml` workflow. Dependency dashboard issue enabled. Explicit semantic commit scopes: `deps(nuget)`, `ci(github-actions)`, `ci(dotnet-sdk)`. Auto-merge covers `digest`, `pin`, `pinDigest`, and `lockFileMaintenance` updateTypes alongside `minor` and `patch`. The three TUnit packages (`TUnit`, `TUnit.Assertions`, `TUnit.Core`) are grouped into a single PR per release.
- Updated `CONVENTIONS.md` to v0.7 (cumulative from v0.5).
- Added `JsonAssertions.TUnit` (the fifth family package, JSON path / value / shape assertions) and `SseAssertions.TUnit` (the sixth family package, Server-Sent Events wire-format assertions) to the `CONVENTIONS.md` family roster.
- Added a per-package strict-scope policy section to `CONVENTIONS.md` with explicit scope statements for all six packages.
- Added a core+adapter packaging rule section to `CONVENTIONS.md`: five of six family packages ship core+adapter; `JsonAssertions.TUnit` is the sole single-package member.
- Synchronized `CONVENTIONS.md` across all six family repos (the file is copied identically).
- Expanded the `README.md` Family roster to six packages, adding `JsonAssertions.TUnit` and `SseAssertions.TUnit` to the "Family compatibility" section, the "Pair with" section, and the "shared across" line in Contributing.
- Added GitHub Actions workflow security scanning. `.github/workflows/zizmor.yml` runs `zizmor` (blocking, with findings shown as inline annotations) on every workflow change; `.github/workflows/codeql.yml` now analyzes the `actions` language alongside `csharp`; `.github/workflows/scorecard.yml` (OpenSSF Scorecard) and `.github/workflows/dependency-review.yml` (fails a PR that adds a high-severity-vulnerable dependency) are new. Added the Renovate `helpers:pinGitHubActionDigestsToSemver` preset so any newly-introduced action is auto-pinned to a commit SHA. CI-only; no effect on shipped packages.

### Security

- Hardened GitHub Actions token handling: set `persist-credentials: false` on every `actions/checkout` so the repository token is not written into `.git/config`; moved the inline coverage-report expression in `ci.yml` into an `env:` variable to remove a template-injection vector; and scoped workflow write permissions (`security-events` on `codeql`; `contents`/`id-token`/`packages`/`attestations` on `release`) to the job level with a read-only workflow-level default. CI-only; no released package is affected.

## [0.4.0] - 2026-05-16: Smart-diff suggestions, renderer pattern, `Scrubbers.Common`

Additive release. No breaking changes; baselines that opted into `Scrubbers.Default` produce byte-identical output.

### Added

- `Scrubbers.GuidN`: scrubs 32-character GUID-N format strings (the `Guid.ToString("N")` shape) into `<guid:N>` tokens. Shares the `"guid"` kind name with `Scrubbers.Guid`, so the index counter is unified across both formats; the Nth GUID occurrence in a snapshot gets the same N regardless of which format produced it.
- `Scrubbers.ElapsedMs`: scrubs elapsed-millisecond values (`42ms`, `42.5ms`, `1234.567 ms`) into `<elapsed-ms:N>` tokens. Case-sensitive on the `ms` suffix.
- `Scrubbers.Common`: curated chain of `Guid` + `GuidN` + `Iso8601Timestamp` + `UnixEpochMillis` + `ElapsedMs`. Superset of `Scrubbers.Default`; opt-in for the extended pattern set. Ordering follows the most-specific-first rule to avoid double-scrubbing.
- `DiffSuggestionAnalyzer.Analyze(string diff)`: scans a snapshot-mismatch diff for known volatile patterns and returns `DiffSuggestion` entries recommending applicable built-in scrubbers, ordered by hit count descending with stable secondary ordering by declaration order. Counts matches only on lines that begin with `+` or `-`; context lines are skipped so patterns unchanged on both sides do not surface as suggestions.
- `DiffSuggestion(string PatternName, int Count, string Recommendation)` record: one scrubber recommendation surfaced by `DiffSuggestionAnalyzer`.
- `SnapshotResult.Describe()` / `WriteDescription` smart-diff suggestion section: on `Mismatched` outcomes with detected patterns, the failure message now includes a "Suggestion(s)" section between the diff and the accept-flow guidance. The list is capped at the top 3 patterns by hit count; surplus patterns roll up into an "... and N more" line pointing consumers at `Scrubbers.Common`. Failure messages for `Mismatched` outcomes with no detected patterns, and for all other outcomes (`Matched`, `NoBaseline`, `Accepted`), are byte-identical to v0.3.0.
- `SnapshotAssertions.Render.SnapshotRenderer<T>` abstract base class: consumers subclass to plug domain types (Google.Protobuf messages, `XDocument`, `Activity`, project-specific value objects) into the snapshot pipeline.
- `SnapshotAssertions.Render.Renderer.For<T>(Func<T, string>)` static factory: builds a renderer from a lambda when a full subclass is unnecessary.
- `RenderedSnapshotAssertion<T>`: renderer-projected assertion type returned by the two new `MatchesSnapshot<T>` overloads. Carries the same chain methods as `SnapshotAssertion` (`WithName`, `AtPath`, `WithOptions`, `WithScrubber`).
- `MatchesSnapshot<T>(this IAssertionSource<T>, SnapshotRenderer<T>)` extension: renderer-projected entry point taking a subclass renderer. Use when the renderer is reusable and owns configuration or state.
- `MatchesSnapshot<T>(this IAssertionSource<T>, Func<T, string>)` extension: delegate-shaped entry point. Use for inline projections and for composing with a sibling family package's static renderer method without taking a reference on its assembly.

### Changed

- `TUnit` package reference bumped `1.44.0` → `1.44.39` (and the external-consumer smoke-test pin). 1.44.39 carries the `[GenerateAssertion]` source-generator fix for value-type optional parameters; no behavioral change for this package, taken for family lockstep.
- `Microsoft.SourceLink.GitHub` bumped `10.0.203` → `10.0.300`. The embedded source-link metadata in shipped `.pdb` files now points at the updated SourceLink schema; debugging-into-the-package from consumers' IDEs is unaffected in behavior but uses the newer SourceLink format.
- README cookbook documents `Scrubbers.Common` ordering rationale, smart-diff suggestion output shape and top-3 cap, the renderer-pattern API with four worked subclass examples (`OtelTraceIdScrubber`, `EphemeralPathScrubber`, `PortScrubber`, `NumericTokenScrubber` as a parameterised variant), and sibling-family composition without cross-package dependency.
- Packaged READMEs (`src/SnapshotAssertions.TUnit/README.md`, `src/SnapshotAssertions/README.md`) mention `Scrubbers.Common`, smart-diff suggestions, and the renderer-pattern API with deep-links to the root README.

## [0.3.0] - 2026-05-12: Scrubbers.Combine + Render namespace + parameterized-test cookbook

Additive release. Surface area grows by one public factory method on `Scrubbers` plus a reserved namespace; no breaking changes; no behavioral changes to existing API.

### Added (SnapshotAssertions, framework-agnostic core)

- **`Scrubbers.Combine(params SnapshotScrubber[])`**: composes an array of scrubbers into a single scrubber that applies them left-to-right. All inner scrubbers share the same `SnapshotScrubberState`, so recurring volatile values keep stable indexed tokens across the combined pipeline (same semantics as chained `.WithScrubber(...)` calls). Returns an identity scrubber for an empty array; returns the single element unchanged for a one-element array; otherwise wraps a defensive copy. Replaces hand-rolled `var s1 = ...; var s2 = ...; var s3 = ...;` patterns and chains of three or more `.WithScrubber(...)` calls per assertion when the same bundle is reused across many tests.
- **`SnapshotAssertions.Render` namespace reserved.** Internal anchor type only; no public surface. Sibling family packages publish their text renderer entry points under this shared namespace in their own assemblies so consumers can discover them with a single `using SnapshotAssertions.Render;`. Convention is namespace-shared, not type-shared: each package owns its renderer types.

### Documentation

- **README cookbook entry for parameterized `[Arguments]` tests.** Pins the file-name convention (`{TestClassName}.{TestMethodName}.{ArgsHash8}.expected.txt`) and documents the `InvariantCulture` stringification behavior for `IFormattable` argument types (so baselines are portable across developer machines and CI regardless of current culture).
- **`Scrubbers.Combine` usage example** added to the "Composing multiple scrubbers" section of the GitHub README and to the packaged README's Scrubbers overview.
- **`CONVENTIONS.md` upgraded to v0.3.** Adds the `SnapshotAssertions.Render` namespace convention so sibling packages have a stable cross-repo target for their text renderers.

### Tests

- **`Scrubbers.Combine` coverage**: empty array → identity, single element → reference equality, multi-element left-to-right composition, shared `SnapshotScrubberState` across inner scrubbers, null-array / null-element argument validation, defensive-copy semantics, identity-scrubber `Apply` null-input / null-state argument validation.
- **TUnit `[Arguments]` integration test** in `MatchesSnapshotChainTests`: pins that two `[Arguments]`-driven rows produce distinct per-row baseline files when calling the no-arg `MatchesSnapshot()` entry point, end-to-end through the TUnit `TestContext` → `SnapshotFileResolver` flow. Unit-level argument-hash coverage was already present at the resolver level.
- **Coverage-improvement tests** for previously-uncovered branches: `ResolveByTest` empty-test-class-name / empty-test-method-name validation; `LineDiffRenderer` expected-longer-than-actual (trailing `-` lines, no `+`) and actual-longer-than-expected (trailing `+` lines, no `-`); `SnapshotComparer` bare-`\r` line-terminator recognition and `IgnoreLineEndings` × `Required` trailing-newline-policy preservation; `SnapshotResult` Mismatched-without-newline-terminated-diff path.

### Refactored

- **`LineDiffRenderer.EmitLine` split into helpers** (`LinesMatch`, `EmitMatchingLine`, `AccumulateDifferingTotal`, `EmitDifferingPair`). Behavior is unchanged; the previous 18-Cyclomatic-Complexity main method is now under the family's 15 threshold and each branch path is independently named for clarity. Public API surface is untouched.

### Quality

- **`PackageValidationBaselineVersion` bumped `0.1.0` → `0.2.0`** for both packages (pins last-released baseline; `Scrubbers.Combine` is additive and auto-suppressed in `CompatibilitySuppressions.xml`).
- **Branch coverage: 94.5%** (139 of 147), up from 0.2.0's 92.25%. Line coverage: 99.4% (533 of 536). Method coverage: 100% (85 of 85). Above both the CI 90% hard gates.
- **Risk Hotspots: no methods exceed Cyclomatic Complexity 15** (was 18 on `LineDiffRenderer.EmitLine` in 0.2.0; the refactor split brings every method comfortably under the family threshold).
- **AOT smoke verified.** Building the external-consumer `SnapshotAssertions.TUnit.SmokeTest` against the packed `0.3.0` nupkgs with `-p:PublishAot=true` produces **0 warnings, 0 errors**, confirming both packages remain AOT-consumer-safe (no new reflection patterns, no IL-level dynamic-code requirements).

### Changed

- **Dependency refresh.** Bumped direct dependencies and analyzer packs to latest stable:
  - `TUnit` / `TUnit.Assertions` / `TUnit.Core`: 1.43.11 → 1.44.0
  - `Microsoft.CodeAnalysis.BannedApiAnalyzers`: 3.3.4 → 4.14.0
  - `Meziantou.Analyzer`: 3.0.72 → 3.0.78
  - `Microsoft.SourceLink.GitHub`: held at 10.0.203 (the 11.x line is preview-only at time of release).
- **`MeziantouAnalysisMode=all-warnings` enabled for `src/` projects** (test projects keep the analyzer defaults; TUnit's instance-method test style and argument-validation idioms like `null!` are by design and would conflict with rules MA0038 / MA0137 / MA0181 / MA0191). The stricter mode caught a handful of pre-existing style improvements: pattern-matching for discrete-value checks (`is`/`is not` over `==`/`!=`), `Convert.ToString(value, InvariantCulture)` over `object.ToString()` on the resolver's argument-stringifier, and `<see langword="…"/>` in XML docs over `<c>…</c>` for keyword literals.
- **`BannedSymbols.txt`**: collapsed standalone `#` comment lines into adjacent text-bearing comment lines. `Microsoft.CodeAnalysis.BannedApiAnalyzers` 4.x is stricter about parsing the file format and treated standalone `#` lines as empty banned-symbol entries.

### Family-wide doc sync

- **Family compatibility count corrected to four packages.** Root `README.md` now references the four shipped family packages (`LogAssertions.TUnit`, `TimeAssertions.TUnit`, `SnapshotAssertions.TUnit`, `MathAssertions.TUnit`) in both the "Family compatibility" CHANGELOG-link list and the "Pair with" entries. Previous text said "three" and omitted `MathAssertions.TUnit`.
- **Packaged README "Family" section added.** The on-NuGet `src/SnapshotAssertions.TUnit/README.md` now mirrors the family-shared packaged-README structure with a `## Family` block linking the three sibling packages (visible on nuget.org).
- **`CONTRIBUTING.md` test-project list expanded.** Enumerates the three test projects (`SnapshotAssertions.TUnit.Tests`, `SnapshotAssertions.TUnit.SnapshotTests`, `SnapshotAssertions.TUnit.SmokeTest`) with their purposes, mirroring the family-shared CONTRIBUTING structure. Previous text mentioned only the main `.Tests` project.
- **Bug-report issue template TUnit-version placeholder bumped `1.43.2` → `1.44.0`** to match the shipped dependency in `Directory.Packages.props`.
- **CHANGELOG header convention aligned with Keep a Changelog 1.1.0**: this entry uses `## [0.3.0] - YYYY-MM-DD: tagline` (date in ISO form). Existing 0.1.0 / 0.2.0 entries kept as-is to avoid post-hoc rewrites.

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

[unreleased]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.6.1...HEAD
[0.6.1]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.6.0...v0.6.1
[0.6.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/releases/tag/v0.1.0
