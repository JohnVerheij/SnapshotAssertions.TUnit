# Family conventions

This document captures conventions shared across the assertion family (`LogAssertions.TUnit`,
`SnapshotAssertions.TUnit`, and the upcoming `HttpAssertions.TUnit` / `TimeAssertions.TUnit`).
The same file is copied identically into each repo. Updates happen in one place
(conceptually) and propagate manually.

## Naming patterns

| Pattern | Purpose | Examples |
|---|---|---|
| `HasX()` | Positive assertion entry point | `HasLogged()`, `HasStatusCode(200)` |
| `HasNotX()` / `IsNotX()` | Negative assertion entry point | `HasNotLogged()`, `IsNotStatusCode(200)` |
| `WithX(...)` | Filter / refinement chained on a parent assertion | `.WithException<T>()`, `.WithPath("/foo")` |
| `IsX()` | Value-shape assertion | `IsOk()`, `IsRecent(TimeSpan)` |
| `AndX()` | Value-returning terminator (returns the matched value) | `AndBody<T>()`, `GetMatch()` |
| `MatchesX(...)` | Comparison against a baseline / snapshot | `MatchesSnapshot()`, `MatchesSnapshotFile(path)` |
| `Dump*(...)` | Non-asserting inspection (writes diagnostic output) | `DumpToTestOutput()`, `DumpTo(TextWriter)` |

## `StringComparison` rule

Every public string-matching API requires the caller to pass `StringComparison` explicitly.
No silent culture defaults. Internal string equality where comparison semantics are unambiguous
(file paths on the platform, line endings) uses `StringComparison.Ordinal`. Meziantou.Analyzer
enforces this via MA0006 / MA0001.

## Async pattern

Every assertion chain is `await`-able end-to-end. No `.Result`, no `.GetAwaiter().GetResult()`,
no sync-over-async. Every async public API accepts `CancellationToken ct = default` (additive
overload where the existing API didn't) — defaulting to `default` keeps existing call-sites
unaffected.

## `TimeProvider` injection convention

Every API that involves waiting, polling, or wall-clock time accepts an optional `TimeProvider`
parameter. When omitted, the default is `TimeProvider.System`. This makes deterministic
fake-time testing (`Microsoft.Extensions.Time.Testing.FakeTimeProvider`) trivial: pass it as
the optional parameter and the assertion uses `timeProvider.GetTimestamp()` /
`timeProvider.GetElapsedTime(...)` for monotonic measurement.

## `[EditorBrowsable(Never)]` on assertion bases

Required-public types (CRTP base classes that exist only to satisfy TUnit's
`[AssertionExtension]` source-generator constraints) are tagged
`[EditorBrowsable(EditorBrowsableState.Never)]` and documented as
"not for external derivation." They appear in the public API surface for binary-compat
reasons but are hidden from IntelliSense.

## Namespace strategy

| Type / member | Namespace | Auto-imported? |
|---|---|---|
| Source-generated assertion entry points (`HasLogged()`, `MatchesSnapshot()`, etc.) | `TUnit.Assertions.Extensions` | Yes — TUnit auto-imports |
| Shorthand entry points | `TUnit.Assertions.Extensions` | Yes — same path |
| Internal types (matchers, options, builders) | Package's own namespace (`SnapshotAssertions`, `LogAssertions`, ...) | No — needs explicit `using` |

## No reflection policy

Family packages use no runtime reflection in the assertion path. The only acceptable
reflection-based code is convenience overloads (e.g. JSON deserialization in HttpAssertions
for non-AOT scenarios), which must be explicitly annotated with `[RequiresUnreferencedCode]`
and `[RequiresDynamicCode]` so AOT consumers see the warning at the call site.

`Microsoft.CodeAnalysis.BannedApiAnalyzers` enforces this at build time via a per-repo
`BannedSymbols.txt` listing reflection APIs.

## Tip-of-tree TFM targeting

At any moment, target current LTS plus current STS during overlap windows. Reset to the new
LTS at SemVer-major version boundaries. Concretely: `net10.0` now → `net10.0;net11.0` at
.NET 11 GA → `net12.0` at .NET 12 GA (major version bump on the package) → `net12.0;net13.0`
at .NET 13 GA, and so on. Aggressive vs Microsoft's official LTS support windows; defensible
because the family explicitly demonstrates modern practices.

## Snapshot tool

The family uses **`SnapshotAssertions.TUnit`** for its own API-surface tests and recommends
it for consumer text-snapshot needs. Verify (Simon Cropp's library) remains the right choice
for object-graph diffing in consumer projects (coexistence, not replacement). Family packages
do not depend on Verify in shipped code.

## Lockstep versioning

Within a single repo, all packages release at the same version, even when one has no API
change. Across the family, versions are independent — `SnapshotAssertions.TUnit 0.1.0` does
not need to align with `LogAssertions.TUnit 0.3.x`.

## Strict ApiCompat

`<EnablePackageValidation>true</EnablePackageValidation>` plus
`<EnableStrictModeForBaselineValidation>true</EnableStrictModeForBaselineValidation>`.
`<PackageValidationBaselineVersion>` pins to the previous shipped version. Adding new APIs
generates `CP0001`/`CP0002` suppressions in `CompatibilitySuppressions.xml`, committed and
accepted as intentional pre-1.0 additions.

## AOT-publish CI gate

Every repo's CI publishes the smoke-test consumer with `dotnet publish -r linux-x64 --aot`
to prove AOT-publishability end-to-end (not just "AOT-compatible by analyzer"). The published
binary must run successfully.

## Version-claim discipline

README states only the supported floor (e.g., "TUnit 1.43.2 or later, .NET 10"). Avoid
archaeological claims about which specific feature shipped in which TUnit minor version
unless verified against TUnit's own release notes / git history. Wrong version claims are
the cheapest way to lose earned credibility.

## Per-package READMEs

Each NuGet package ships a short, focused README in the `.nupkg` (`src/<Package>/README.md`).
The root `README.md` is the comprehensive reference rendered by GitHub on the repo home;
nuget.org gets the focused 60-second view that links out to the full reference.

## Naming discipline (upstream content)

Never name internal organizations or projects (employer, internal product names, internal
adopter names) in any public-facing content — README, CHANGELOG, commit messages, PR bodies,
issue descriptions. Use generic adoption-anchor framing.
