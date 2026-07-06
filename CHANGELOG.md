# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> *History note:* All version sections were reformatted on 2026-07-05 for one-time
> [CONVENTIONS &sect;CHANGELOG](CONVENTIONS.md) conformance: forbidden sub-headers folded into
> the six Keep a Changelog headers, non-user-facing content removed (internal refactors, test
> counts, coverage numbers, CI and build hygiene, governance churn, roadmap notes), bullets
> kept in past-tense active voice with code-formatted API leads. The nuget.org Release Notes
> tab and the GitHub Release for each shipped version are unchanged. A CI `family-lint` gate
> keeps future sections conforming; each is frozen per Rule 7 once shipped.

## [Unreleased]

## [0.7.1] - 2026-06-14: restore the packed build targets

Patch release. Restores `build/SnapshotAssertions.TUnit.targets` to the package. 0.7.0 shipped without it, so a consumer's committed `Snapshots/*.expected.txt` baselines stopped copying to the test output directory and every snapshot assertion failed with "baseline does not exist". No public API change.

### Fixed

- **The package again ships `build/SnapshotAssertions.TUnit.targets`.** DotNetProjectFile.Analyzers 1.15.0 added `**/*.targets` to a SonarQube content glob marked `Pack="false"`, which silently overrode this package's explicit `Pack="true"` for its own build targets and dropped the file from the 0.7.0 `.nupkg`. That targets file auto-includes a consumer's `Snapshots/**/*.expected.txt` with `CopyToOutputDirectory="PreserveNewest"`, so without it the committed baselines never reach the test binary's output directory and the resolver reports them missing. Setting `SonarQubeIntegration=false` (the integration is unused in this package) restores the packed asset, and a CI check now asserts the produced `.nupkg` contains the build targets so it cannot silently drop again.

**Upgrade:** Consumers who applied the 0.7.0 workaround (a manual `<None Update="Snapshots/**/*.expected.txt" CopyToOutputDirectory="PreserveNewest" />` together with `<SnapshotAssertionsAutoIncludeSnapshots>false</SnapshotAssertionsAutoIncludeSnapshots>`) can remove both after upgrading; the package's own targets resume handling the copy. Leaving the workaround in place stays harmless.

## [0.7.0] - 2026-06-14: correctness hardening

Minor release. Three correctness and robustness fixes to the comparison, file resolution, and write paths. No public API change.

### Fixed

- **Ordinal line-ending mode now preserves the original terminators.** When `SnapshotLineEndingMode.Ordinal` was combined with any option that engages line-by-line normalization (trailing-whitespace trimming or a non-Required trailing-newline policy), the canonical form rejoined every line with `Environment.NewLine`. That made an accepted baseline's bytes depend on the platform accept-mode ran on (CRLF on Windows, LF on Linux) and silently discarded the exact endings Ordinal mode exists to pin. Each line's original terminator (CRLF, LF, or bare CR) is now preserved, so the canonical form is platform-independent and faithful.
- **Distinct collection arguments no longer collide onto one snapshot file.** A parameterized test whose arguments include an array or other collection hashed all variants of a given type to the same value (a collection has no value-based `ToString`, so it returned the type name, for example `System.Int32[]`). The variants then shared one baseline file and raced each other's `.actual` writes. Collection arguments are now expanded element-by-element (recursively, for nested collections), so each variant resolves to its own file. Scalar and string arguments hash exactly as before.
- **Baseline and actual files are written atomically.** Writes now go to a uniquely-named temporary file that is moved over the target, so a partial or interrupted write cannot leave a half-written baseline, and two writers racing the same target no longer throw a sharing violation mid-write under parallel test execution.

**Upgrade:** The collection-argument fix changes the hashed file name for parameterized snapshots whose arguments include a collection (those were colliding before, so their baselines were already unreliable). Re-accept those snapshots after upgrading. Snapshots with only scalar or string arguments, and all non-parameterized snapshots, are unaffected.

## [0.6.2] - 2026-06-12: clearer failure when snapshots differ only in line endings

Patch release. Improves the failure diagnostic for a line-ending-only mismatch. No public API change.

### Fixed

- **The diff now explains a line-ending-only mismatch instead of rendering an empty diff.** When a snapshot failed only because of its line endings (for example a CRLF baseline against LF actual), the line-by-line view consumed the endings, so every line matched and the diff showed no `+`/`-` markers under a "did not match" header. The renderer now detects this case and appends a hint naming each side's detected endings (for example `expected: CRLF, actual: LF`) and how to normalize them.

## [0.6.1] - 2026-06-05: document source-tree accept under --no-build

Patch release. Documentation correction; no code, public API, or behavior change.

### Changed

- README accept-changes workflow corrected for the `0.6.0` behavior: `SNAPSHOT_ACCEPT=1` writes the new baseline directly into the **source** `Snapshots/` folder (resolved via `SnapshotFileResolver.TryResolveSourceSnapshotsDirectory`), so the committed baseline updates in place even under `dotnet test --no-build`. The prior text still described the pre-`0.6.0` "manually move the bin-directory files into source" step; it now documents the source-tree write with the bin-directory write called out only as the fallback when the source path cannot be resolved.

## [0.6.0] - 2026-06-04: source-tree accept resolution and baseline-generation fixes

Minor release. Adds a public source-tree resolution helper used by accept-mode, plus two behavior fixes to the baseline-generation path. Both fixes correct cases where a first-run or accepted baseline ended up in a form the next comparison would not read back as written.

### Added

- **`SnapshotFileResolver.TryResolveSourceSnapshotsDirectory(string startDirectory)`** walks up from a runtime directory (typically `AppContext.BaseDirectory`) to the nearest ancestor project directory and returns its `Snapshots/` folder, or `null` when no source tree is present. Accept-mode uses it to write an accepted baseline into the committable source tree rather than the runtime copy under `bin/`. Additive.

### Fixed

- **`SnapshotOptions.WithNormalizer` baseline candidate** is now written in normalized form. Previously, on a first run with no baseline (and on accept-mode), the persisted candidate was the raw, un-normalized subject, so a consumer who accepted it committed un-canonicalized or unscrubbed volatile text that the very next comparison normalized away. The candidate (the `.actual.txt` a consumer renames to accept, and the `.expected.txt` accept-mode overwrites) now carries the same option-driven normalized form the comparison reads back.
- **`SNAPSHOT_ACCEPT=1` accept target** now resolves to the source-tree `Snapshots/` directory rather than the runtime copy under `bin/`. Previously, under `dotnet test --no-build`, accepting wrote the `.expected.txt` next to the runtime location, so it never reached the committable `tests/.../Snapshots/` source folder and CI later reported the baseline as missing. Accept-mode now walks up from `AppContext.BaseDirectory` to the nearest ancestor project directory and writes there, so accepting lands the baseline where it is committed and read from. The read path is unchanged; only the accept write target moves.

## [0.5.0] - 2026-06-03: SnapshotOptions.WithNormalizer

Feature release. Adds `SnapshotOptions.WithNormalizer(Func<string, string>)`, a caller-supplied transform applied to both the actual content and the expected baseline before any built-in normalization. It generalizes the built-in line-ending normalizer into an arbitrary pre-comparison transform: canonicalize JSON or XML, sort nondeterministic collections, mask volatile fields, or reformat numbers before the snapshot compares.

### Added

- **`SnapshotOptions.WithNormalizer(Func<string, string> normalizer)`** returns a copy of the options with a transform applied to both sides before BOM, line-ending, trailing-whitespace, and trailing-newline handling. Chaining composes in registration order (the first registered runs first). The transform sees the raw rendered text, so it is the natural seam for canonicalizing content whose textual form is noisy but semantically irrelevant. The backing **`SnapshotOptions.Normalizer`** property (a `Func<string, string>?`, default `null`) is also public for direct construction. A normalizer that returns `null` fails with an `InvalidOperationException` rather than a downstream `NullReferenceException`.
- **`Scrubbers.IndexedPattern(Regex pattern, string kind)`** replaces every match with `<kind:N>`, where recurring identical matched values share the same index N and distinct values get incrementing indices in first-occurrence order. It is the indexed counterpart to `Scrubbers.Pattern`: where `Pattern` is flat (every match collapses to one literal token, losing correlation), `IndexedPattern` reuses the same index machinery as the built-in indexed scrubbers. Passing a built-in `kind` (e.g. `"guid"`) shares that built-in's index counter.
- **`Scrubbers.IndexedPattern(string pattern, string kind)`** compiles `pattern` with `RegexOptions.NonBacktracking | RegexOptions.CultureInvariant` and otherwise behaves identically to the `Regex` overload, mirroring the existing `Scrubbers.Pattern(string, string)` ergonomics.

### Changed

- Added a README cookbook recipe, "Snapshotting serialized/binary formats via a canonical renderer": render the decoded wire bytes canonically (one field per line) through the existing `SnapshotRenderer<T>` path rather than snapshotting `ToString()` / JSON, so the assertion is a wire test rather than a content test. The recipe stays format-agnostic (protobuf / XML / MessagePack).

## [0.4.0] - 2026-05-16: smart-diff suggestions, renderer pattern, Scrubbers.Common

Additive release. No breaking changes; baselines that opted into `Scrubbers.Default` produce byte-identical output.

### Added

- **`Scrubbers.GuidN`**: scrubs 32-character GUID-N format strings (the `Guid.ToString("N")` shape) into `<guid:N>` tokens. Shares the `"guid"` kind name with `Scrubbers.Guid`, so the index counter is unified across both formats.
- **`Scrubbers.ElapsedMs`**: scrubs elapsed-millisecond values (`42ms`, `42.5ms`, `1234.567 ms`) into `<elapsed-ms:N>` tokens. Case-sensitive on the `ms` suffix.
- **`Scrubbers.Common`**: curated chain of `Guid` + `GuidN` + `Iso8601Timestamp` + `UnixEpochMillis` + `ElapsedMs`. Superset of `Scrubbers.Default`; opt-in for the extended pattern set. Ordering follows the most-specific-first rule to avoid double-scrubbing.
- **`DiffSuggestionAnalyzer.Analyze(string diff)`**: scans a snapshot-mismatch diff for known volatile patterns and returns `DiffSuggestion` entries recommending applicable built-in scrubbers, ordered by hit count descending with stable secondary ordering by declaration order. Counts matches only on lines beginning with `+` or `-`.
- **`DiffSuggestion(string PatternName, int Count, string Recommendation)`** record: one scrubber recommendation surfaced by `DiffSuggestionAnalyzer`.
- **`SnapshotResult.Describe()` / `WriteDescription` smart-diff suggestion section**: on `Mismatched` outcomes with detected patterns, the failure message now includes a "Suggestion(s)" section between the diff and the accept-flow guidance, capped at the top 3 patterns by hit count with surplus rolled into an "... and N more" line pointing at `Scrubbers.Common`. Messages for `Mismatched` with no detected patterns, and for all other outcomes, are byte-identical to 0.3.0.
- **`SnapshotAssertions.Render.SnapshotRenderer<T>`** abstract base class: consumers subclass to plug domain types (Google.Protobuf messages, `XDocument`, `Activity`, project-specific value objects) into the snapshot pipeline.
- **`SnapshotAssertions.Render.Renderer.For<T>(Func<T, string>)`** static factory: builds a renderer from a lambda when a full subclass is unnecessary.
- **`RenderedSnapshotAssertion<T>`**: renderer-projected assertion type returned by the two new `MatchesSnapshot<T>` overloads. Carries the same chain methods as `SnapshotAssertion` (`WithName`, `AtPath`, `WithOptions`, `WithScrubber`).
- **`MatchesSnapshot<T>(this IAssertionSource<T>, SnapshotRenderer<T>)`** extension: renderer-projected entry point taking a subclass renderer.
- **`MatchesSnapshot<T>(this IAssertionSource<T>, Func<T, string>)`** extension: delegate-shaped entry point, for inline projections and composing with a sibling family package's static renderer method without taking a reference on its assembly.

### Changed

- Bumped `TUnit` / `TUnit.Assertions` / `TUnit.Core` `1.44.0` -> `1.44.39` (carries the `[GenerateAssertion]` source-generator fix for value-type optional parameters; no behavioral change for this package, taken for family lockstep).
- Bumped `Microsoft.SourceLink.GitHub` `10.0.203` -> `10.0.300`. The embedded source-link metadata in shipped `.pdb` files uses the newer SourceLink format; debugging into the package from a consumer IDE is behavior-unchanged.
- README cookbook now documents `Scrubbers.Common` ordering rationale, the smart-diff suggestion output shape and top-3 cap, and the renderer-pattern API with four worked subclass examples.

## [0.3.0] - 2026-05-12: Scrubbers.Combine and the Render namespace

Additive release. Surface area grows by one public factory method on `Scrubbers` plus a reserved namespace; no breaking changes, no behavioral changes to existing API.

### Added

- **`Scrubbers.Combine(params SnapshotScrubber[])`**: composes an array of scrubbers into a single scrubber that applies them left-to-right. All inner scrubbers share the same `SnapshotScrubberState`, so recurring volatile values keep stable indexed tokens across the combined pipeline. Returns an identity scrubber for an empty array; returns the single element unchanged for a one-element array; otherwise wraps a defensive copy.
- **`SnapshotAssertions.Render` namespace reserved.** Internal anchor type only; no public surface. Sibling family packages publish their text renderer entry points under this shared namespace in their own assemblies so consumers can discover them with a single `using SnapshotAssertions.Render;`. Convention is namespace-shared, not type-shared: each package owns its renderer types.

### Changed

- Bumped `TUnit` / `TUnit.Assertions` / `TUnit.Core` `1.43.11` -> `1.44.0`.
- README cookbook gained an entry for parameterized `[Arguments]` tests, pinning the file-name convention (`{TestClassName}.{TestMethodName}.{ArgsHash8}.expected.txt`) and the `InvariantCulture` stringification behavior for `IFormattable` argument types, plus a `Scrubbers.Combine` usage example in the "Composing multiple scrubbers" section.

## [0.2.0] - 2026-05-07: built-in scrubbers and dependency refresh

Feature release. Lockstep version bump for both packages; ApiCompat baseline pinned to 0.1.0.

### Added

- **`SnapshotScrubber` abstract base + `Scrubbers` static factory** for transforming snapshot text before comparison. Replaces volatile substrings (GUIDs, timestamps, epoch millis) with stable indexed tokens so a baseline survives multiple test runs.
- **Five built-in scrubbers**: `Scrubbers.Guid` (8-4-4-4-12 hex GUIDs, case-insensitive, `<guid:N>`); `Scrubbers.Iso8601Timestamp` (ISO 8601 timestamps with optional fractional seconds and `Z` / `+/-HH:MM` zone, `<iso8601:N>`); `Scrubbers.UnixEpochMillis` (13-digit Unix-millis at word boundaries, `<unixms:N>`); `Scrubbers.Pattern(Regex, string)` and `Scrubbers.Pattern(string, string)` (every regex match replaced with the literal token; the string overload compiles with `RegexOptions.NonBacktracking | RegexOptions.CultureInvariant`).
- **`Scrubbers.Default`**: curated chain of `Guid + Iso8601Timestamp + UnixEpochMillis` for the common case.
- **Indexed-token format (`<kind:N>`)**: recurring volatile values share an index (first-occurrence order, per kind); different kinds keep independent counters; state is per-snapshot evaluation and never crosses test boundaries.
- **`SnapshotScrubberState`**: public per-snapshot state object (a kind / value -> index map) used by indexed scrubbers. Consumers extending `SnapshotScrubber` reuse it via `state.GetOrAssignIndex(kind, value)`.
- **`MatchesSnapshot(...).WithScrubber(SnapshotScrubber)`** chain method (TUnit adapter). Multiple `WithScrubber` calls compose left-to-right and share a single `SnapshotScrubberState` across the whole snapshot.

### Changed

- Bumped `TUnit` / `TUnit.Assertions` / `TUnit.Core` `1.43.2` -> `1.43.11`.

## [0.1.0] - 2026-05-05: initial release, text-snapshot assertions for TUnit

First public release. Two packages ship in lockstep: `SnapshotAssertions` (framework-agnostic core) and `SnapshotAssertions.TUnit` (TUnit adapter). Net 10, AOT-compatible, trimmable, no runtime reflection.

### Added

- **`SnapshotComparer`**: pure string-against-string comparison with option-driven normalization (line endings, BOM, trailing whitespace, trailing newline). Stateless; callable from any test framework.
- **`SnapshotOptions`** (sealed record) plus four enum types (`SnapshotLineEndingMode`, `SnapshotBomHandling`, `SnapshotTrailingWhitespace`, `SnapshotTrailingNewline`). Strict-by-default (`SnapshotOptions.Default`); cross-platform preset via `SnapshotOptions.NormalizedLineEndings`.
- **`SnapshotEvaluator`**: orchestrates a single comparison: reads the expected baseline, invokes the comparer, applies accept-mode when active, writes the actual file on mismatch / no-baseline, and returns a `SnapshotResult`.
- **`SnapshotFileResolver`**: pure path construction (`ResolveByName`, `ResolveByTest`, `ResolveByFile`, `GetDefaultSnapshotsDirectory`).
- **`SnapshotAcceptMode`**: env-var-driven accept logic, active when `SNAPSHOT_ACCEPT` is truthy AND `CI` is not, plus a pure `IsActive(snapshotAcceptValue, ciValue)` overload for deterministic testing.
- **`LineDiffRenderer`**: line-by-line diff with unified-diff-style prefixes (` `/`-`/`+`), truncated to 20 differing lines for very large snapshots.
- **`SnapshotResult`** + **`SnapshotMatchOutcome`** (`Matched`, `Mismatched`, `NoBaseline`, `Accepted`) plus `SnapshotPaths` record-struct.
- **`SnapshotException`** carrying the `SnapshotResult`; its message is the same human-readable form rendered by `SnapshotResult.Describe()`.
- **`SnapshotAssertion`** (TUnit adapter): `Assertion<string>` with `[AssertionExtension("MatchesSnapshot")]` generating `Assert.That(actualText).MatchesSnapshot()`, with `.WithName(string)`, `.AtPath(string)`, and `.WithOptions(SnapshotOptions)` chain methods.
- **Shorthand entry-point extensions** in `TUnit.Assertions.Extensions`: `MatchesSnapshot(string)`, `MatchesSnapshot(SnapshotOptions)`, `MatchesSnapshot(string, SnapshotOptions)`, `MatchesSnapshotFile(string)`, `MatchesSnapshotFile(string, SnapshotOptions)`.
- **Zero-config project setup**: the adapter ships `build/SnapshotAssertions.TUnit.targets`, auto-imported by NuGet to include `Snapshots/**/*.expected.txt` with `CopyToOutputDirectory="PreserveNewest"`; no csproj wiring. Opt out with `<SnapshotAssertionsAutoIncludeSnapshots>false</SnapshotAssertionsAutoIncludeSnapshots>`.
- **Default file resolution**: `MatchesSnapshot()` without `.WithName` / `.AtPath` reads `TestContext.Current` to build `{AppContext.BaseDirectory}/Snapshots/{TestClassName}.{TestMethodName}.expected.txt`, writing a sibling `.actual.txt` on mismatch and throwing `InvalidOperationException` when called outside a TUnit test context.
- **Accept-changes workflow**: accept a baseline per-snapshot (copy `.actual.txt` over `.expected.txt`) or in bulk (`SNAPSHOT_ACCEPT=1` then `dotnet test`); the accept guard refuses when `CI=true` so a pipeline cannot silently accept drift.
- **AOT-compatible, trimmable, no runtime reflection** in the assertion path.

[unreleased]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.7.1...HEAD
[0.7.1]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.7.0...v0.7.1
[0.7.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.6.2...v0.7.0
[0.6.2]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.6.1...v0.6.2
[0.6.1]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.6.0...v0.6.1
[0.6.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/releases/tag/v0.1.0
