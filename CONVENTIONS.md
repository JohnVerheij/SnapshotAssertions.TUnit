# Code conventions

Rules for how code is written across the assertion family (`LogAssertions.TUnit` and
`SnapshotAssertions.TUnit`). The same file is copied identically into each repo.

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
reflection-based code is convenience overloads (e.g. JSON deserialization for non-AOT
scenarios), which must be explicitly annotated with `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]` so AOT consumers see the warning at the call site.

`Microsoft.CodeAnalysis.BannedApiAnalyzers` enforces this at build time via a per-repo
`BannedSymbols.txt` listing reflection APIs.
