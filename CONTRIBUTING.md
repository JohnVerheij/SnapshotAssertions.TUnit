# Contributing to SnapshotAssertions.TUnit

Thanks for considering a contribution. This document covers the small set of conventions that keep the project consistent.

## Reporting bugs

Use the [bug report template](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/issues/new?template=bug_report.yml). The template asks for the SnapshotAssertions.TUnit version, TUnit version, .NET SDK version, OS, expected vs actual behavior, and a minimal reproduction. The smaller and more self-contained the repro, the faster the fix.

## Proposing features

Use the [feature request template](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/issues/new?template=feature_request.yml). Describe the test scenario the feature enables, the proposed API shape (in code), and any alternatives you considered. Indicate whether you'd like to submit a PR yourself.

For larger ideas (new entry points, breaking changes, cross-cutting refactors), open a [Discussion](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/discussions) first to align on direction before investing implementation time.

## Submitting a pull request

1. Fork the repo and create a branch from `main`. Branch name convention: `fix/short-description`, `feat/short-description`, `docs/short-description`.
2. Make your change. Keep it focused: a single logical change per PR.
3. Add or update tests. The project uses TUnit; existing tests in `tests/SnapshotAssertions.TUnit.Tests/` show the patterns.
4. Build clean: `dotnet build` must produce zero warnings (`TreatWarningsAsErrors=true` is enforced).
5. Test green: `dotnet test` must pass.
6. Update `CHANGELOG.md` under the `## [Unreleased]` section.
7. Open the PR. Use the [PR template](.github/PULL_REQUEST_TEMPLATE.md).

## Code conventions

- **Language version:** C# 14 (`<LangVersion>14.0</LangVersion>`). Use modern C# (collection expressions, primary constructors where appropriate, etc.).
- **Nullability:** enabled. All public APIs annotate nullability correctly.
- **XML documentation:** required on every public type, member, and parameter.
- **Argument validation:** public methods that take reference-type arguments use `ArgumentNullException.ThrowIfNull(...)` at the top of the body. Methods taking numeric arguments validate ranges with `ArgumentOutOfRangeException.ThrowIfNegative(...)` (or similar).
- **Spelling:** American English in code, identifiers, and prose. (TUnit upstream uses British; this project does not. Don't take this as a stylistic preference, just a project rule.)
- **Globalization:** explicit `CultureInfo.InvariantCulture` on all `string.Format`, numeric `ToString`, and similar calls. Meziantou.Analyzer enforces this via MA0011.
- **String comparison:** explicit `StringComparison.Ordinal` (or `OrdinalIgnoreCase` where appropriate). Meziantou.Analyzer enforces this via MA0006.
- **No reflection:** the package does not use runtime reflection. Adding any usage of `MethodBase.Invoke`, `PropertyInfo.GetValue/SetValue`, `Activator.CreateInstance(Type)`, or `Type.GetMethod`/`Type.GetProperty` requires explicit justification in PR review.

## Tests

Test projects (under `tests/`):

- `SnapshotAssertions.TUnit.Tests/`: the main TUnit-adapter test suite, exercising the `MatchesSnapshot()` / `MatchesSnapshotFile()` chain, the `WithName` / `AtPath` / `WithOptions` / `WithScrubber` methods, and the `Scrubbers` / `SnapshotScrubberState` / `SnapshotFileResolver` framework-agnostic core via TUnit `[Test]` cases.
- `SnapshotAssertions.TUnit.SnapshotTests/`: self-test that pins the public API surface of both packages by piping `PublicApiGenerator` output through `MatchesSnapshot()` against checked-in `.expected.txt` baselines. Dogfooding: the snapshot tool tests itself on its own surface.
- `SnapshotAssertions.TUnit.SmokeTest/`: minimal one-test consumer-install verification project, used to validate that the `.nupkg` packs and resolves correctly against a fresh consumer csproj.

General test rules:

- Each public method on the assertion classes should have at least one test covering its happy path and at least one covering an invalid-input path.
- Tests use TUnit's `[Test]` and the project's own assertion style (we eat our own dog food where possible).
- Add `[Category("Smoke")]` to tests that should run in the pre-commit / fast feedback loop.

## Snapshot files (when contributing tests)

Tests that use `MatchesSnapshot()` produce two file types:

- `*.expected.txt`: the committed baseline. Diffed against actual output on every run.
- `*.actual.txt`: the transient diff output, written when actual diverges from expected. Gitignored; never commit.

To accept a snapshot change:

1. Locally, use your IDE's diff-and-merge view, or `cp Snapshots/Foo.actual.txt Snapshots/Foo.expected.txt`.
2. Or run `SNAPSHOT_ACCEPT=1 dotnet test` to bulk-accept all changes.
3. CI never sets `SNAPSHOT_ACCEPT`; mismatches fail the build.

## Releases

Versioning follows [Semantic Versioning](https://semver.org/):
- `0.x.y` while the API is evolving: minor bumps may include breaking changes.
- `1.0.0` and beyond: breaking changes only on major-version bumps.

Releases are published to NuGet via a tagged commit on `main`. The `<Version>` in `SnapshotAssertions.TUnit.csproj` is the source of truth for the package version. Both packages (`SnapshotAssertions` and `SnapshotAssertions.TUnit`) ship in lockstep on every tag.

## Code of conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to abide by its terms.

## License

By contributing, you agree that your contributions will be licensed under the same [MIT License](LICENSE) that covers the project.
