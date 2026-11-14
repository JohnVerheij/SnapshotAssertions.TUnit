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

### Changed

- **Targets .NET 10 and .NET 11.** Both packages ship `net10.0` and `net11.0` assets; a test project that targets both runs each test on both frameworks against one shared baseline per default name.
- **`SnapshotComparer`, `SnapshotEvaluator` and `MatchesSnapshot()`** now reject a `SnapshotOptions.LineEndingMode` value outside the enum with an `ArgumentOutOfRangeException` (`Unknown LineEndingMode value: N`) instead of comparing it as some other mode. This is what the removed `IgnoreLineEndings` value (`3`) does if a compiled caller still passes it.
- **`WithScrubber(...)`** now fails with an `InvalidOperationException` that names the scrubber when a custom scrubber returns `null`, as a normalizer that returns `null` already did, instead of an `ArgumentNullException` about `actualContent` or `input`.
- **README samples** now compile against the packed packages: the renderer, regular expression, XML, JSON and public API samples gained the `using` lines they were missing, and the file-name section states two limits of the default name (a generic test class resolves one name for every type argument, and a record, tuple or `KeyValuePair` row holding a number or date is not culture independent) and that a normalizer has to be idempotent.

### Removed

- **BREAKING:** `SnapshotLineEndingMode.IgnoreLineEndings`, which was marked obsolete, is removed. Use `NormalizeToLF`, which keeps every line break in the comparison; a baseline that an earlier version accepted as one line has to be accepted again.

### Fixed

- **`SnapshotTrailingNewline.Optional`** (and so `SnapshotOptions.NormalizedLineEndings`): content that ends with a blank line, such as `"a\n\n"`, could never be accepted into a passing state. The accepted baseline lost its final line break when written, and the comparison then dropped a second one when it read the file back, so every run reported a mismatch. The written file now keeps the final line break in that case; other content is written as before.
- **`SnapshotEvaluator.EvaluateAsync`** (and so `MatchesSnapshot()`) no longer fails on Windows when two evaluations of one snapshot meet, for example the `net10.0` and `net11.0` test processes of one `dotnet test` run accepting the same project baseline, or tests that share a default name: a baseline that is being read or replaced by another evaluation, or an actual file that is being deleted, is retried for a short bounded time instead of throwing an `IOException` or `UnauthorizedAccessException`. A missing file or directory, a path that is too long and a missing drive are not retried and fail at once with their own exception.
- **Accepting into a read-only baseline** on Windows now throws an `UnauthorizedAccessException` that names the file, where the operating system's message did not.

## [0.9.0] - 2026-11-06: line-ending preset, class-argument naming and accept-mode fixes

Minor release. `SnapshotOptions.NormalizedLineEndings` now means "CRLF and CR become LF, the final newline is ignored" instead of removing line breaks, and class-level `[Arguments]` rows get their own default-named baseline files; both are breaking for the baselines they affect, and each entry says how to migrate. `SnapshotLineEndingMode.IgnoreLineEndings` is deprecated and its written form keeps line breaks. The accept-mode CI guard also reads `TF_BUILD` and honors `SNAPSHOT_ACCEPT_IN_CI`, accept mode writes into the project's `Snapshots/` folder when the output is elsewhere, `PreserveBom` now keeps the baseline's byte-order mark, the accept-mode fallback and `TryResolveSourceSnapshotsDirectory` now recognize `.fsproj` and `.vbproj`, the build targets are packed under `buildTransitive/`, and the failure message names the project-side baseline.

### Added

- **`SnapshotFileResolver.ResolveByTest(string, string, IReadOnlyList<object?>?, string, IReadOnlyList<object?>?)`** also hashes the value-like class-level (constructor) arguments into the name: `{Class}.{ClassArgsHash8}.{Method}[.{MethodArgsHash8}]`. Value-like means `null`, a string, a `bool`, a `char`, a `Type`, an `IFormattable` (all numeric types, `Guid`, `DateTime`, enums), or an array or other collection that implements the non-generic `ICollection` (such as `List<T>`) of these (recursively); any other argument, such as a `[ClassDataSource]` fixture, a lazy sequence or a `HashSet<T>` (it implements only the generic `ICollection<T>`), is skipped without being enumerated, and a collection with one non-value-like element, such as a dictionary with entries, is skipped as a whole. With no value-like class argument the name is exactly the four-parameter overload's, which stays unchanged. The TUnit adapter uses the new overload for default-named snapshots.
- **`SnapshotAcceptMode.AzurePipelinesVariableName`** (`TF_BUILD`) and **`SnapshotAcceptMode.AllowInCiVariableName`** (`SNAPSHOT_ACCEPT_IN_CI`) are the names of the two new variables the CI guard reads.
- **`SnapshotAcceptMode.IsActive(string?, string?, string?, string?)`** is the pure four-variable form (`SNAPSHOT_ACCEPT`, `CI`, `TF_BUILD`, `SNAPSHOT_ACCEPT_IN_CI`) that `IsActive()` now uses. A truthy `SNAPSHOT_ACCEPT_IN_CI` lifts the CI guard, so a pipeline step that regenerates baselines on purpose sets it instead of overriding `CI`. The two-variable overload keeps its meaning.

### Changed

- **BREAKING:** `SnapshotOptions.NormalizedLineEndings` is now `LineEndingMode = NormalizeToLF` with `TrailingNewline = Optional`: CRLF and CR become LF, the final newline is ignored, and every line break still counts. It used to remove line breaks altogether, so `"a\nb"` equalled `"ab"`.
- **BREAKING:** The default-named baselines of class-level `[Arguments]` rows now include a hash of the class arguments in the name (`{Class}.{ClassArgsHash8}.{Method}`), so each row gets its own file; they used to share one. A class whose only constructor argument is a fixture object, and method-only `[Arguments]` rows, keep their names. The `.WithName(...)` workaround from 0.8.1 is now optional.
- **`SnapshotLineEndingMode.IgnoreLineEndings`** written form (the actual file and an accepted baseline) now keeps the line breaks, joined with LF, instead of one flattened line. Its comparison is unchanged, so a baseline accepted earlier as one line still matches.
- **Failure message** of a default-named or `.WithName(...)` baseline now ends with one more line, `Project baseline: <path>`, the absolute path the baseline has under the project's `Snapshots/` folder, when the build recorded the project directory and that folder still exists. The core message is unchanged, and a baseline set with `AtPath` or `MatchesSnapshotFile` gets no such line.
- **`TUnit.Assertions`** and **`TUnit.Core`** minimum versions raised from 1.78.0 to 1.80.0.

### Deprecated

- **`SnapshotLineEndingMode.IgnoreLineEndings`** is marked `[Obsolete]`: it also ignores where lines break and blank lines. Use `NormalizeToLF`. It will be removed in 0.10.0.

### Fixed

- **Accept mode** now counts a truthy `TF_BUILD` (Azure Pipelines sets `True`) as CI, so a stray `SNAPSHOT_ACCEPT=1` on an Azure Pipelines agent is refused like it is where `CI` is set. Before, only `CI` was read, and Azure Pipelines does not set it, so accept mode ran there.
- **`SnapshotBomHandling.PreserveBom`** now keeps a byte-order mark at the start of the baseline file: the file is read as bytes and a UTF-8, UTF-16 or UTF-32 mark is returned as a leading U+FEFF, so a baseline accepted from an actual that starts with U+FEFF matches on the next run. The mark was consumed by the read, so it never matched. `StripBom` behaves as before, and a file in UTF-16 or UTF-32 is decoded with the encoding its mark declares.
- **Accept mode** (`SNAPSHOT_ACCEPT=1`) now writes into the project's `Snapshots/` folder when the build output is outside the project (`UseArtifactsOutput`, `dotnet test -o`, a custom `OutputPath`). The package's build targets record the project directory in the test assembly's runtime configuration (`SnapshotAssertions.ProjectDirectory`), and accept mode reads it. Before, it wrote beside the test binary or into an unrelated project higher up.
- **`SnapshotFileResolver.TryResolveSourceSnapshotsDirectory`** and the accept-mode fallback now also recognize `*.fsproj` and `*.vbproj`, so accept mode finds the project of an F# or Visual Basic test assembly when the build did not record the project directory.
- **Build targets** are now packed under `buildTransitive/` (with a one-line forwarding file in `build/`), so a test project that gets the package through another project or package also gets the copy of `Snapshots/**/*.expected.txt` to the output directory, unless the intermediate reference sets `PrivateAssets="all"` or excludes the build assets. Before, only a direct `PackageReference` did.

**Upgrade:** Re-accept (`SNAPSHOT_ACCEPT=1`) a baseline that was accepted under the old `NormalizedLineEndings` preset: it is a single line. A multi-line baseline written by hand keeps matching when its line structure matches the output. Re-accept class-level `[Arguments]` rows too: they now get their own files, where they used to share one. Fixture-only classes and method-only `[Arguments]` rows keep their names, so their baselines need no change.

## [0.8.1] - 2026-11-04: document using existing golden files

Patch release. Documentation, the accept guidance in the failure messages and the TUnit dependency floor; no API change.

### Changed

- **`MatchesSnapshotFile`** documentation now says a baseline is the plain expected text in any file, so the golden files a suite already keeps work as they are (UTF-8, or the encoding a leading byte-order mark declares). A new README section, "Using existing golden files", maps a hand-rolled golden helper to the package (a fixture folder in the source tree, comparison after line-ending normalization, an update flag, a missing-golden error, an actual file on failure). The package READMEs and the `SnapshotAssertions.TUnit` description say the same.
- **`SnapshotOptions.NormalizedLineEndings`** and **`SnapshotLineEndingMode.IgnoreLineEndings`** documentation now says line breaks are removed before comparing, so blank lines and where lines break are ignored. `IgnoreLineEndings` keeps a final newline as one LF unless the trailing-newline policy is `Optional`; the preset sets `Optional`, so its written actual file and accepted baseline have no line breaks.
- **`SnapshotLineEndingMode.NormalizeToLF`** is now what the cross-platform recipes in the cookbook and troubleshooting use (`SnapshotOptions.Default with { LineEndingMode = SnapshotLineEndingMode.NormalizeToLF }`), instead of `SnapshotOptions.NormalizedLineEndings`.
- **`SnapshotLineEndingMode.Ordinal`** documentation now says that under `SnapshotTrailingNewline.Optional` the final line's ending is not compared.
- **`SnapshotBomHandling`** and **`SnapshotOptions.Default`** documentation now says a byte-order mark at the start of the baseline file is consumed when the file is read and never compared, and that `BomHandling` applies to a leading U+FEFF on both compared sides. It previously said the default preserves the BOM or compares everything as-is, and that `PreserveBom` makes a file's BOM count.
- **`SnapshotFileResolver.ResolveByFile`** XML docs now name the actual file for a custom path (`.actual` inserted before the extension, or appended when there is none; the `.expected.txt` match is case-sensitive) instead of always `.actual.txt`; the `SnapshotResult`, `SnapshotAssertion`, `SnapshotEvaluator` and `SnapshotFileResolver` docs refer to it.
- **`.gitignore`** recommendation in the README now ignores `*.actual.*` and `*.actual` instead of `*.actual.txt`, so the actual files of custom paths are ignored too, and keeps `*.expected.txt`, so an `.expected.txt` baseline whose name contains `.actual.` (such as `.WithName("Order.actual")`) is still committed. The broader patterns can also match a custom-path baseline or folder whose name contains `.actual`; the README shows how to check with `git check-ignore -v`.
- **`SnapshotFileResolver.TryResolveSourceSnapshotsDirectory`** documentation now gives the rule accept mode uses to find the source `Snapshots/` folder: the nearest folder above the test binary that contains a `.csproj` file (`.fsproj` and `.vbproj` are not recognized), so an output directory outside the project folder (for example `UseArtifactsOutput`) writes elsewhere. The README previously said the source location was compiled in.
- **`SNAPSHOT_ACCEPT`** documentation now says accept mode writes the normalized content as UTF-8 and also creates missing baselines, describes the CI guard as `CI` being `1`, `true` or `yes` (Azure Pipelines does not set it), and shows how a pipeline step that regenerates baselines on purpose sets `CI` to a non-truthy value for that step only. The bulk accept also shows the PowerShell form.
- **`SNAPSHOT_ACCEPT`** bootstrapping guidance in the README now says a new test's default-named baseline is written into the `Snapshots/` folder next to the nearest `.csproj` above the test binary, normally the source folder. It previously said to move the file back from the test binary's directory.
- **`SnapshotAssertions.TUnit.targets`** guidance in the README now says NuGet imports the build targets by default only into a project that references `SnapshotAssertions.TUnit` directly, and to add a direct `PackageReference` when the test project gets the package through another project.
- **`.gitattributes`** guidance in the README now includes converting baselines that are already committed and checked out.
- **`.gitattributes`** guidance in the README now limits the `text eol=lf` rule to UTF-8 files: Git converts line endings byte by byte, so a UTF-16 or UTF-32 file is converted to UTF-8 first or kept out of the rule.
- **`SnapshotAssertionsAutoIncludeSnapshots`** opt-out in the README now shows the full `<None>` item: `CopyToOutputDirectory`, and a `Link` into the output's `Snapshots/` folder where default-named baselines are read. It also says accept mode still writes to the project's `Snapshots/` folder. It previously said to add a `<None Include>` without either.
- **`[Arguments]`** documentation now says class-level (constructor) arguments are not part of the hash in the snapshot file name, so rows that differ only in those arguments share one baseline file. To separate them, pass `.WithName(...)` a name built from the class and method names, the class arguments and any method arguments, because an explicit name replaces the whole default name.
- **`SnapshotAssertions`** package README now describes `SnapshotComparer`, `SnapshotEvaluator`, `SnapshotFileResolver` and `SnapshotAcceptMode` as they behave.
- **`SnapshotAssertions`** package README stability note now says a minor version before 1.0 can contain breaking changes, while renaming or removing one of the intended-stable types listed in the main README needs a major version, as the main README says.
- **`SnapshotAssertions`** package README, its NuGet description and the main README now say adapters for other test frameworks (NUnit, xUnit, MSTest) are not planned. The package README and the NuGet description previously listed them as possible if there was demand, and the main README invited a feature request for one.
- **`TUnit.Assertions`** and **`TUnit.Core`** minimum versions raised from 1.77.0 to 1.78.0.

### Fixed

- **`SnapshotResult.Describe`**: the accept guidance in the failure messages now says to copy the actual file to the expected path and, for an `.expected.txt` file in the build output's `Snapshots/` folder, to the matching path in the project's `Snapshots/` folder. The no-baseline message said to rename the actual file to `.expected.txt`, which does not create a baseline at a custom path with another extension (such as `insert-line.golden.txt`), and for a default-named snapshot both messages pointed at the copy in the build output, which left the committed baseline unchanged.

## [0.8.0] - 2026-11-01: Forbidden trailing-newline policy enforced

Minor release. `SnapshotTrailingNewline.Forbidden` now enforces the check its documentation described, and the TUnit dependency floor moves to 1.77.0. No public API shape change.

### Changed

- **BREAKING:** `SnapshotTrailingNewline.Forbidden` now fails the comparison when the expected baseline or the actual content ends with a trailing newline, including when both do and the content is otherwise identical. It previously behaved exactly like `Optional`: both sides had a trailing newline stripped before comparison, so its presence was unobservable, and the README recorded the two values as producing the same outcome. Normalization under `Forbidden` now preserves the trailing-newline state, as `Required` does, so a written `.actual.txt` or an accepted baseline keeps the trailing newline of the normalized content, and the failure output names the offending side and the alternatives.
- `SnapshotTrailingNewline.Required` and `SnapshotOptions.TrailingNewline` documentation now describes the implemented behavior: the trailing-newline state of both sides is preserved and compared, and a difference fails the comparison. The previous wording said the baseline must end with a newline, which was never checked on its own.
- `TUnit.Assertions` and `TUnit.Core` minimum versions raised from 1.54.0 to 1.77.0.

**Upgrade:** Consumers on `Forbidden` whose baseline or producer ends with a trailing newline now fail. Remove the trailing newline from the producer and re-accept the baseline, or choose `Optional` (presence is ignored) or `Required` (presence is preserved and compared). `Optional` and `Required` behavior is unchanged.

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

[unreleased]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.9.0...HEAD
[0.9.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.8.1...v0.9.0
[0.8.1]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.8.0...v0.8.1
[0.8.0]: https://github.com/JohnVerheij/SnapshotAssertions.TUnit/compare/v0.7.1...v0.8.0
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
