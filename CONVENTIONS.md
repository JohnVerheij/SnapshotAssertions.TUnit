# Code conventions

Rules for how code is written across the assertion family (`LogAssertions.TUnit`,
`SnapshotAssertions.TUnit`, `TimeAssertions.TUnit`, `MathAssertions.TUnit`, and
`JsonAssertions.TUnit`). The same file is copied identically into each repo.

**Document version:** v0.4 (2026-05-14). Changes from v0.3: added `JsonAssertions.TUnit` to
the family roster (the fifth package; JSON path / value / shape assertions over
`System.Text.Json`).

**Document version:** v0.3 (2026-05-12). Changes from v0.2: added the `SnapshotAssertions.Render`
namespace reservation for sibling-package text renderers so consumers discover renderer
entry points via a single `using SnapshotAssertions.Render;`.

**Document version:** v0.2 (2026-05-07). Changes from v0.1: codified the family rule against
promoting Verify; added polling-loop default-schedule agreement; added `ToSnapshotString()`
format-version header rule; added test-projects-only scope blockquote as a binding
cross-repo convention; codified TFM policy (LTS-anchored; multi-target during STS support
windows); expanded the `CancellationToken` plumbing rule with provider-driven polling-sleep
semantics.

## Naming patterns

| Pattern | Purpose | Examples |
|---|---|---|
| `HasX()` | Positive assertion entry point | `HasLogged()`, `HasStatusCode(200)` |
| `HasNotX()` / `IsNotX()` | Negative assertion entry point | `HasNotLogged()`, `IsNotStatusCode(200)` |
| `WithX(...)` | Filter / refinement chained on a parent assertion | `.WithException<T>()`, `.WithPath("/foo")` |
| `IsX()` | Value-shape assertion | `IsOk()`, `IsRecent(TimeSpan)` |
| `AndX()` | Value-returning terminator (returns the matched value) | `AndBody<T>()`, `GetMatch()` |
| `MatchesX(...)` | Comparison against a baseline / snapshot | `MatchesSnapshot()`, `MatchesSnapshotFile(path)` |
| `WithinTimeBudget(TimeSpan)` | Cross-cutting timing budget on any chain (compose via `.And`) | `.And.WithinTimeBudget(TimeSpan.FromMilliseconds(500))` |
| `Dump*(...)` | Non-asserting inspection (writes diagnostic output) | `DumpToTestOutput()`, `DumpTo(TextWriter)` |

## `StringComparison` rule

Every public string-matching API requires the caller to pass `StringComparison` explicitly.
No silent culture defaults. Internal string equality where comparison semantics are unambiguous
(file paths on the platform, line endings) uses `StringComparison.Ordinal`. Meziantou.Analyzer
enforces this via MA0006 / MA0001.

## Async pattern + `CancellationToken` plumbing

Every assertion chain is `await`-able end-to-end. No `.Result`, no `.GetAwaiter().GetResult()`,
no sync-over-async. Every async public API accepts `CancellationToken ct = default` (additive
overload where the existing API didn't); defaulting to `default` keeps existing call-sites
unaffected.

For polling, looping, or internal-timeout APIs, the additional rules are:

- Call `ct.ThrowIfCancellationRequested()` at the top of every poll iteration. Don't wait
  for the next sleep to surface cancellation.
- For sleep / delay between iterations, use `Task.Delay(interval, ct)` for cancellation
  cleanup. When a `TimeProvider?` is supplied non-null on the API, see the polling-loop
  default-schedule section below for the provider-driven variant.
- For internal-timeout APIs (e.g. `WithinHardTimeBudget(TimeSpan)`), create the internal
  `CancellationTokenSource(timeout)` and link it with the supplied external CT via
  `CancellationTokenSource.CreateLinkedTokenSource(externalCt, internalCts.Token)`. Either
  source firing aborts the operation; consumer-side intent is preserved.

## `TimeProvider` injection convention

Every API that involves waiting, polling, or wall-clock time accepts an optional `TimeProvider`
parameter. When omitted, the default is `TimeProvider.System`. This makes deterministic
fake-time testing (`Microsoft.Extensions.Time.Testing.FakeTimeProvider`) trivial: pass it as
the optional parameter and the assertion uses `timeProvider.GetTimestamp()` /
`timeProvider.GetElapsedTime(...)` for monotonic measurement.

`TimeAssertions.TUnit` is the canonical implementation of this convention. Every sibling
package's timing-related API accepts `TimeProvider` independently; no shared dependency.

## Polling-loop default-schedule agreement

`LogAssertions.WithinTimeout` and `TimeAssertions.Eventually` (and any future polling
terminator across the family) follow an explicit, fully-pinned schedule. Each package
implements independently (the family rule forbids cross-package code reference); the
convention pins the schedule so consumers see uniform behaviour without literal code
sharing.

**Schedule.** Exponential schedule: 100ms, 200ms, 400ms, 800ms, then 1000ms cap. Escalates
one step per failed poll. Resets to 100ms on a true poll. Both axes pinned (multiplier and
trigger) so two independent implementations cannot drift in cadence.

**Provider-driven polling sleep.** When the supplied `TimeProvider?` parameter is non-null,
the polling sleep MUST use `Task.Delay(interval, timeProvider, ct)` (the static `Task.Delay`
overload accepting a `TimeProvider`, available .NET 8+) rather than `Task.Delay(interval,
ct)`. This is required for `FakeTimeProvider` to drive the polling loop deterministically: a
wall-clock `Task.Delay` ignores `Advance(...)` and the loop never re-evaluates the predicate
when the consumer expected fake-time progression to do so. When `TimeProvider?` is null,
falls back to `Task.Delay(interval, ct)`. If `Task.Delay(TimeSpan, TimeProvider,
CancellationToken)` doesn't satisfy the polling shape for some future requirement, fall back
to a timer-built wait via `timeProvider.CreateTimer(...)` plus a `TaskCompletionSource`. Same
rule applies to `EveryWindow`, `WithinHardTimeBudget`, and any future polling/timer-driven
family API.

## `ToSnapshotString()` format-version header

Family rendering helpers (e.g. `LogAssertions.ToSnapshotString()`) emit a fixed header line
as the first line of the output: `# <Package> snapshot v<N>`. The header is part of the
deterministic format, not metadata: it appears in every committed snapshot.

Format-version bumps (added field, reordered output, etc.) increment the version number
(`v2`, `v3`...) and are **always a major-version bump on the package itself**. Consumers'
committed snapshots therefore carry an explicit format-version marker that survives `git
diff` review and lets a future `MatchesSnapshot` rendering detect format-incompatibility
cleanly rather than silently failing on a one-line drift.

## `[EditorBrowsable(Never)]` on assertion bases

Required-public types (CRTP base classes that exist only to satisfy TUnit's
`[AssertionExtension]` source-generator constraints) are tagged
`[EditorBrowsable(EditorBrowsableState.Never)]` and documented as
"not for external derivation." They appear in the public API surface for binary-compat
reasons but are hidden from IntelliSense.

## Namespace strategy

| Type / member | Namespace | Auto-imported? |
|---|---|---|
| Source-generated assertion entry points (`HasLogged()`, `MatchesSnapshot()`, `WithinTimeBudget()`, `IsApproximatelyEqualTo()`, etc.) | `TUnit.Assertions.Extensions` | Yes (TUnit auto-imports) |
| Shorthand entry points | `TUnit.Assertions.Extensions` | Yes (same path) |
| Internal types (matchers, options, builders) | Package's own namespace (`SnapshotAssertions`, `LogAssertions`, `TimeAssertions`, `MathAssertions`, `JsonAssertions`, ...) | No (needs explicit `using`) |
| Text renderer entry points: types whose role is to project a domain object into a deterministic string for `MatchesSnapshot()` | `SnapshotAssertions.Render` | No (needs `using SnapshotAssertions.Render;`) |

### `SnapshotAssertions.Render` for sibling-package renderers

Sibling family packages publish their text renderers under the shared `SnapshotAssertions.Render` namespace in their own assemblies. The shape is namespace-shared, not type-shared: each package owns its renderer types, and the types co-exist by sharing the namespace name across assemblies. Cross-assembly partial classes do not compose, so no package publishes a "renderer hub" static class for siblings to extend.

`SnapshotAssertions` itself reserves the namespace via an internal anchor type. The convention exists to give consumers a single `using SnapshotAssertions.Render;` directive that surfaces renderer entry points from every family package present in the test project.

## No reflection policy

Family packages use no runtime reflection in the assertion path. The only acceptable
reflection-based code is convenience overloads (e.g. JSON deserialization for non-AOT
scenarios), which must be explicitly annotated with `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]` so AOT consumers see the warning at the call site.

`Microsoft.CodeAnalysis.BannedApiAnalyzers` enforces this at build time via a per-repo
`BannedSymbols.txt` listing reflection APIs.

## Test-projects-only scope

Every README in every family repo opens with the blockquote:

```markdown
> **Scope:** Test projects only. Not intended for production code.
```

This is binding across:
- The repo-level root `README.md`
- Each per-package `src/<Package>/README.md` (the one packed into the `.nupkg` and shown
  on nuget.org)

The scope statement appears immediately after the H1 title (and after CI badges in the
root README, before the package description).

## TFM policy

Family packages always target the **current LTS** of .NET. While a non-LTS (STS) release is in
support, packages multi-target the current LTS *plus* the current STS. When the next LTS ships,
both the previous LTS and the previous STS are dropped on the same release; the new LTS becomes
the single target until its STS sibling appears the following November.

| Window (approximate dates) | Target frameworks |
|---|---|
| Now, .NET 10 LTS only (Nov 2025 to Nov 2026) | `net10.0` |
| .NET 11 STS in support (Nov 2026 to Nov 2027) | `net10.0;net11.0` |
| .NET 12 LTS ships, drop 10 + 11 (Nov 2027 to Nov 2028) | `net12.0` |
| .NET 13 STS in support (Nov 2028 to Nov 2029) | `net12.0;net13.0` |
| ... | ... |

The TFM rotation lands at major-version boundaries (`2.0`, `3.0`, ...). Consumers who need an
older TFM pin to an older package version. Wide multi-targeting (e.g. `net8;net9;net10`) is not
used; the goal is "current LTS, plus current STS while it exists" with no long historical tails.

## Verify is not promoted

The family does NOT promote [Verify](https://github.com/VerifyTests/Verify) in any
documentation, plan, README, or example. Rendering helpers (e.g. `ToSnapshotString()`)
produce framework-agnostic strings; the canonical example pipes to
`Assert.That(s).MatchesSnapshot()` (using `SnapshotAssertions.TUnit`), never to
`await Verify(s)`.

Verify is acceptable in consumer code that needs object-graph diffing (its core
competency); the family coexists with Verify but does not actively recommend or push
consumers toward it. The reason: `SnapshotAssertions.TUnit` exists specifically to
provide a coverage-friendly, AOT-first text-snapshot tool that avoids the Verify+MTP
coverage interaction on Linux runners, and promoting Verify in family documentation
would directly contradict that founding rationale.
