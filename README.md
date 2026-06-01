# SnapshotAssertions.TUnit

[![CI](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/actions/workflows/codeql.yml/badge.svg)](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/JohnVerheij/SnapshotAssertions.TUnit/badge)](https://scorecard.dev/viewer/?uri=github.com/JohnVerheij/SnapshotAssertions.TUnit)
[![codecov](https://codecov.io/gh/JohnVerheij/SnapshotAssertions.TUnit/branch/main/graph/badge.svg)](https://codecov.io/gh/JohnVerheij/SnapshotAssertions.TUnit)
[![NuGet](https://img.shields.io/nuget/v/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![Downloads](https://img.shields.io/nuget/dt/SnapshotAssertions.TUnit.svg)](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

A TUnit-native fluent text-snapshot assertion library built on TUnit's `[AssertionExtension]` source generator. AOT-compatible, trimmable, no runtime reflection. Coexists with [Verify](https://github.com/VerifyTests/Verify): does not replace it for object-graph diffing.

> **Scope:** Test projects only. Not intended for production code.

---

## Table of contents

- [Why this package](#why-this-package)
- [Install](#install)
- [Package layout](#package-layout)
- [Namespaces (and a `GlobalUsings.cs` recommendation)](#namespaces-and-a-globalusingscs-recommendation)
- [Quick start](#quick-start)
- [Snapshot file conventions](#snapshot-file-conventions)
- [Project setup (`csproj` wiring)](#project-setup-csproj-wiring)
- [Entry points](#entry-points)
  - [Source-generated entry](#source-generated-entry)
  - [Chain methods](#chain-methods)
  - [Shorthand entry-point extensions](#shorthand-entry-point-extensions)
- [Comparison options](#comparison-options)
  - [`SnapshotLineEndingMode`](#snapshotlineendingmode)
  - [`SnapshotBomHandling`](#snapshotbomhandling)
  - [`SnapshotTrailingWhitespace`](#snapshottrailingwhitespace)
  - [`SnapshotTrailingNewline`](#snapshottrailingnewline)
  - [Constructing customized options](#constructing-customized-options)
- [Scrubbers (volatile value handling)](#scrubbers-volatile-value-handling)
  - [Built-in scrubbers](#built-in-scrubbers)
  - [The `Scrubbers.Default` preset](#the-scrubbersdefault-preset)
  - [Indexed-token format and recurring-value handling](#indexed-token-format-and-recurring-value-handling)
  - [`Scrubbers.Pattern` for custom regex matches](#scrubberspattern-for-custom-regex-matches)
  - [Composing multiple scrubbers](#composing-multiple-scrubbers)
  - [Custom scrubbers](#custom-scrubbers)
- [Failure diagnostics](#failure-diagnostics)
  - [Mismatched baseline](#mismatched-baseline)
  - [No baseline](#no-baseline)
- [Accept-changes workflow](#accept-changes-workflow)
- [Cookbook: common patterns](#cookbook--common-patterns)
- [Comparison with Verify](#comparison-with-verify)
  - [Migrating from Verify.TUnit](#migrating-from-verifytunit)
- [Troubleshooting](#troubleshooting)
- [Modern .NET 10+ practices on display](#modern-net-10-practices-on-display)
- [Design notes](#design-notes)
- [Stability intent (pre-1.0)](#stability-intent-pre-10)
- [Limitations and future work](#limitations-and-future-work)
- [Family compatibility](#family-compatibility)
- [Pair with](#pair-with)
- [Contributing](#contributing)
- [License](#license)

---

## Why this package

For TUnit projects with API-surface snapshot tests (`PublicApiGenerator` → committed `.expected.txt`), the existing options are:

- **Verify (`Verify.TUnit`)**: feature-rich, but its `Verify.props` forces `<Deterministic>false</Deterministic>` (Verify needs absolute PDB paths to find `.verified.txt` files). On Linux runners that interaction breaks `Microsoft.CodeCoverage`'s instrumentation pipeline and produces an empty 178-byte cobertura skeleton, which Codecov rejects as "Unusable". The interaction is documented at [TUnit#4149](https://github.com/thomhurst/TUnit/discussions/4149).
- **Hand-rolled file compare**: `PublicApiGenerator` + `File.ReadAllText` + `string.Equals`. Works, but every project re-invents the same 30-50 lines of accept-flow / file-naming / diff-display scaffolding.

`SnapshotAssertions.TUnit` covers the **text-snapshot 80% case** (string → file comparison) without the coverage friction or the per-project boilerplate. Object-graph diffing and IDE-integrated diff display remain explicitly out of scope; use Verify when you need those. Built-in scrubbing of dynamic content (GUIDs, ISO 8601 timestamps, Unix-epoch-millis numbers, custom regex patterns) ships in v0.2.0+: see [Scrubbers](#scrubbers-volatile-value-handling).

## Install

```bash
dotnet add package SnapshotAssertions.TUnit
```

**Requirements:** TUnit 1.48.0 or later, .NET 10. `SnapshotAssertions` (the framework-agnostic core) comes transitively. The package is AOT-compatible, trimmable, and uses no runtime reflection in the assertion path.

## Package layout

This repo ships **two** NuGet packages:

| Package | Purpose | Depends on |
|---|---|---|
| [`SnapshotAssertions`](https://www.nuget.org/packages/SnapshotAssertions/) | Framework-agnostic core: `SnapshotComparer`, `SnapshotEvaluator`, `SnapshotFileResolver`, `SnapshotAcceptMode`, `LineDiffRenderer`, options types | BCL only |
| [`SnapshotAssertions.TUnit`](https://www.nuget.org/packages/SnapshotAssertions.TUnit/) | TUnit-specific entry points: `MatchesSnapshot()`, `MatchesSnapshotFile()` and shorthands | `SnapshotAssertions` + `TUnit.Assertions` + `TUnit.Core` |

You install `SnapshotAssertions.TUnit`; `SnapshotAssertions` comes transitively. Adapters for other test frameworks (NUnit, xUnit, MSTest) are *not* shipped today: they would reuse the `SnapshotAssertions` core. Open a feature request if you need one.

## Namespaces (and a `GlobalUsings.cs` recommendation)

The two packages place types in two namespaces with deliberately-different scopes:

| Type / member | Namespace | Auto-imported? |
|---|---|---|
| `MatchesSnapshot()`, `MatchesSnapshotFile()` (source-generated entries + shorthand extensions) | `TUnit.Assertions.Extensions` | **Yes**: TUnit auto-imports |
| `SnapshotOptions`, `SnapshotLineEndingMode`, `SnapshotBomHandling`, `SnapshotTrailingWhitespace`, `SnapshotTrailingNewline` | `SnapshotAssertions` | **No**: needs `using SnapshotAssertions;` |
| `SnapshotComparer`, `SnapshotEvaluator`, `SnapshotFileResolver`, `SnapshotAcceptMode`, `LineDiffRenderer`, `SnapshotResult`, `SnapshotPaths`, `SnapshotException` | `SnapshotAssertions` | **No**: same path |
| `SnapshotAssertion` (CRTP-style assertion class) | `SnapshotAssertions.TUnit` | **No**: only needed for explicit-import / advanced scenarios |

**Practical consequence:** test files that *only* call `Assert.That(text).MatchesSnapshot()` need no `using` from this package. Files that pass `SnapshotOptions` or call helpers like `SnapshotFileResolver.GetDefaultSnapshotsDirectory(...)` need `using SnapshotAssertions;`.

**Recommended:** put both into a single `GlobalUsings.cs` in your test project so every test file sees them without ceremony:

```csharp
// tests/MyApp.Tests/GlobalUsings.cs
global using SnapshotAssertions;
```

This eliminates the IDE0005 ("unnecessary using") chatter that otherwise appears in test files that don't directly use `SnapshotOptions` but live alongside ones that do.

## Quick start

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

That is the entire test. The default file resolver writes
`Snapshots/{TestClassName}.{TestMethodName}.expected.txt` relative to the test binary's
output directory (`AppContext.BaseDirectory`). On mismatch, `*.actual.txt` is written next
to the expected file and the assertion failure message includes both paths plus a
line-based diff.

## Snapshot file conventions

**Default folder location:** `Snapshots/` next to your test binary at runtime. Source files
live under `tests/<YourProject>/Snapshots/` and are copied to the binary directory by your
csproj (see "Project setup" below).

**Default file name:** `{TestClassName}.{TestMethodName}.expected.txt`, derived from
`TestContext.Current.Metadata.TestDetails.ClassType.Name` and `.MethodName`. Examples:

```text
Snapshots/PublicApiTests.SnapshotAssertionsPublicApiHasNotChanged.expected.txt
Snapshots/HostEndpointsTests.PredictReturnsBadRequest.expected.txt
```

**Custom name:** pass an explicit name when one test produces multiple snapshots:

```csharp
await Assert.That(beforeRender).MatchesSnapshot("Order.before-render");
await Assert.That(afterRender).MatchesSnapshot("Order.after-render");
```

**Custom path:** pass an absolute or relative file path to bypass the convention entirely:

```csharp
await Assert.That(payload).MatchesSnapshotFile("contract/v1/payload.expected.txt");
```

**File extensions:**

- `.expected.txt`: the committed baseline. Diffed against actual output on every test run.
- `.actual.txt`: transient diff output, written next to the expected file when the actual content does not match. Gitignored; never commit.

## Project setup (`csproj` wiring)

**No csproj wiring needed.** The package ships a `build/SnapshotAssertions.TUnit.targets`
file that NuGet auto-imports into the consuming project. Any `.expected.txt` file under your
project's `Snapshots/` folder is automatically copied to the test binary's output directory
at every build (`CopyToOutputDirectory="PreserveNewest"`). Just install the package and start
writing tests.

To opt out (e.g., if you keep snapshots in a non-default location and want full manual
control), set this property in your csproj:

```xml
<PropertyGroup>
  <SnapshotAssertionsAutoIncludeSnapshots>false</SnapshotAssertionsAutoIncludeSnapshots>
</PropertyGroup>
```

Then add a `<None Include="...">` matching your custom layout.

Recommended `.gitignore` (transient `.actual.txt` files must not be committed):

```gitignore
*.actual.txt
```

Recommended `.gitattributes` so cross-platform runs don't produce false-positive
trailing-CRLF diffs against committed baselines:

```gitattributes
*.expected.txt text eol=lf
*.actual.txt   text eol=lf
```

## Entry points

### Source-generated entry

`Assert.That(string).MatchesSnapshot()`: the no-arg entry generated by TUnit's
`[AssertionExtension("MatchesSnapshot")]` attribute on the `SnapshotAssertion` class. Returns
a `SnapshotAssertion` that you can chain on, or `await` directly to fail-fast on the default
behavior.

### Chain methods

Methods on the `SnapshotAssertion` returned from `MatchesSnapshot()`:

| Method | Effect |
|---|---|
| `.WithName(string snapshotName)` | Override the default test-context-derived snapshot name. The file becomes `Snapshots/{snapshotName}.expected.txt`. Path separators in the name are rejected. |
| `.AtPath(string filePath)` | Override path resolution entirely with an explicit absolute or relative file path to the expected baseline. Relative paths resolve against the current working directory. |
| `.WithOptions(SnapshotOptions options)` | Override the comparison options (line-ending handling, BOM, trailing whitespace, trailing newline). Default is `SnapshotOptions.Default` (strict). |
| `.WithScrubber(SnapshotScrubber scrubber)` *(v0.2.0+)* | Append a text-transform pass that runs before comparison. Multiple `.WithScrubber()` calls compose left-to-right; the first scrubber receives the raw actual content, each subsequent scrubber receives the previous scrubber's output. See [Scrubbers (volatile value handling)](#scrubbers-volatile-value-handling). |

### Shorthand entry-point extensions

For common patterns, shorthand extensions in `TUnit.Assertions.Extensions` save the chain
ceremony:

| Shorthand | Equivalent chain |
|---|---|
| `Assert.That(text).MatchesSnapshot("MyName")` | `.MatchesSnapshot().WithName("MyName")` |
| `Assert.That(text).MatchesSnapshot(options)` | `.MatchesSnapshot().WithOptions(options)` |
| `Assert.That(text).MatchesSnapshot("MyName", options)` | `.MatchesSnapshot().WithName("MyName").WithOptions(options)` |
| `Assert.That(text).MatchesSnapshotFile("/path.txt")` | `.MatchesSnapshot().AtPath("/path.txt")` |
| `Assert.That(text).MatchesSnapshotFile("/path.txt", options)` | `.MatchesSnapshot().AtPath("/path.txt").WithOptions(options)` |

All shorthands return a `SnapshotAssertion`, so you can still chain further (for example,
`.MatchesSnapshot("MyName").WithOptions(...)` if you want to combine).

## Comparison options

`SnapshotOptions` is a sealed record with strict-by-default values. Two presets are provided:

- `SnapshotOptions.Default`: strict-default (no normalization; preserve everything as-is).
- `SnapshotOptions.NormalizedLineEndings`: convenience preset for cross-platform tests
  where LF/CRLF differences are not meaningful.

Beyond the presets, `WithNormalizer(Func<string, string>)` (v0.5.0+) attaches a caller-supplied transform that runs on both the actual content and the expected baseline *before* any built-in normalization. Use it to canonicalize content whose textual form is noisy but semantically irrelevant: reformat JSON or XML, sort a nondeterministic collection, mask volatile fields, or normalize number formatting. Chaining composes in registration order (the first registered runs first), and the committed baseline stores the canonical form, so a reordering or reformatting that does not change meaning is not a diff.

```csharp
await Assert.That(body).MatchesSnapshot(SnapshotOptions.Default
    .WithNormalizer(text => Canonicalize(text)));
```

The four configurable properties:

### `SnapshotLineEndingMode`

How line-ending differences between the actual content and the expected baseline are handled.

| Value | Behavior |
|---|---|
| `Ordinal` (default) | No normalization. Bytes are compared as-is. CRLF and LF are different. |
| `NormalizeToLF` | Both sides normalized to LF before comparison. Cross-platform-safe. |
| `NormalizeToCRLF` | Both sides normalized to CRLF before comparison. Windows-leaning. |
| `IgnoreLineEndings` | Line endings stripped from both sides entirely; only line content is compared. |

### `SnapshotBomHandling`

How a leading byte-order-mark is handled.

| Value | Behavior |
|---|---|
| `StripBom` (default) | A UTF-8 BOM is stripped from both sides before comparison. |
| `PreserveBom` | BOM bytes are treated as part of the content. |

### `SnapshotTrailingWhitespace`

How per-line trailing whitespace is treated.

| Value | Behavior |
|---|---|
| `Preserve` (default) | Trailing whitespace differences fail the match. |
| `TrimTrailingPerLine` | Trailing whitespace trimmed from each line on both sides before comparison. |

### `SnapshotTrailingNewline`

How the trailing newline at end-of-file is treated.

| Value | Behavior |
|---|---|
| `Required` (default) | Preserve the trailing-newline state of the input. If actual lacks a trailing newline but expected has one (or vice versa), the comparison reports a mismatch. |
| `Optional` | Both sides normalized to no trailing newline; presence vs absence is unobservable to the comparison. |
| `Forbidden` | Same equality outcome as `Optional` today. (Future versions may add a stricter check that fails if either side has a trailing newline.) |

### Constructing customized options

Use C# record `with` syntax to build a customized set:

```csharp
var options = SnapshotOptions.Default with
{
    LineEndingMode = SnapshotLineEndingMode.NormalizeToLF,
    TrailingWhitespace = SnapshotTrailingWhitespace.TrimTrailingPerLine,
};

await Assert.That(actual).MatchesSnapshot(options);
```

## Scrubbers (volatile value handling)

> *Available from v0.2.0.*

Snapshots that contain values which change run-to-run (GUIDs, ISO 8601 timestamps, Unix-epoch-millis numbers, request IDs, generated session keys, machine-specific paths) are otherwise unmaintainable: every run produces a different actual file, every diff is noise, every reader of the snapshot baseline learns to ignore "the parts that change". Scrubbers fix this by transforming the actual content before comparison: volatile substrings are replaced with stable indexed tokens (`<guid:0>`, `<iso8601:1>`, etc.) so the baseline survives multiple test runs, while the test still fails when the *non-volatile* content changes.

Scrubbers compose left-to-right via `.WithScrubber(...)` on the `MatchesSnapshot()` chain:

```csharp
using SnapshotAssertions;

await Assert.That(jsonResponse)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Default);
```

The `Scrubbers` static factory exposes the built-ins; `SnapshotScrubber` is the abstract base class for custom scrubbers.

### Built-in scrubbers

Three indexed scrubbers cover the common volatile-value cases. Each emits tokens of the form `<kind:N>` where N is assigned by first-occurrence order within the snapshot, per kind. **Recurring values share the same N**: if a particular GUID appears three times, every occurrence renders as `<guid:0>`; a different GUID becomes `<guid:1>`.

| Built-in | Matches | Token format | Notes |
|---|---|---|---|
| `Scrubbers.Guid` | 8-4-4-4-12 hex GUIDs (case-insensitive) | `<guid:N>` | Index look-up uses the lower-case canonical form, so mixed-case occurrences of the same GUID share an index. |
| `Scrubbers.Iso8601Timestamp` | ISO 8601 timestamps with optional fractional seconds and `Z` / `±HH:MM` zone | `<iso8601:N>` | Comparison is ordinal: different precisions or offsets get different indices. Date-only and time-only forms are not matched. |
| `Scrubbers.UnixEpochMillis` | 13-digit numbers with non-zero leading digit at word boundaries | `<unixms:N>` | Covers epoch-ms from 2001-09-09T01:46:40Z (1_000_000_000_000) through year ~2286 (max 13-digit value 9_999_999_999_999 ms ≈ 316.88 years from 1970). Leading-zero / all-zero 13-digit tokens are rejected. 12- or 14-digit numbers are not matched. |

```csharp
var input = "id=11111111-2222-3333-4444-555555555555 ts=2026-05-07T13:45:30Z ms=1746619530000";

await Assert.That(input)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Guid)
    .WithScrubber(Scrubbers.Iso8601Timestamp)
    .WithScrubber(Scrubbers.UnixEpochMillis);
// Compared content: "id=<guid:0> ts=<iso8601:0> ms=<unixms:0>"
```

### The `Scrubbers.Default` preset

For the common case, `Scrubbers.Default` chains all three built-ins in deterministic order (`Guid` → `Iso8601Timestamp` → `UnixEpochMillis`):

```csharp
await Assert.That(jsonResponse)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Default);
```

The order is unobservable when the underlying patterns do not overlap (a GUID can't be a timestamp can't be a 13-digit number), so `Scrubbers.Default` is equivalent to chaining the three built-ins individually.

### Indexed-token format and recurring-value handling

The indexed format (`<kind:N>`) means: **first occurrence of a value gets index 0, the second distinct value gets index 1, and so on, per kind.** Subsequent occurrences of an earlier value reuse its index.

```csharp
var input = """
    user-id=abcdef00-1111-2222-3333-444444444444
    request-id=11111111-2222-3333-4444-555555555555
    user-id=ABCDEF00-1111-2222-3333-444444444444
    """;

// Compared content (line endings normalized for prose):
//   user-id=<guid:0>
//   request-id=<guid:1>
//   user-id=<guid:0>      ← case-insensitive match; same index as first occurrence
```

Different kinds maintain **independent** index counters: `<guid:0>`, `<iso8601:0>`, and `<unixms:0>` can all coexist in the same snapshot: index 0 is per-kind, not per-snapshot.

Indexing state is **per-snapshot evaluation**: state lives only for the duration of a single `MatchesSnapshot()` call. State never crosses test boundaries: even within the same test run, two `.MatchesSnapshot()` calls on different fields each get fresh state.

### `Scrubbers.Pattern` for custom regex matches

Two overloads of `Scrubbers.Pattern(...)` cover values not handled by the built-ins. **No indexing** is applied: every match becomes the same literal token.

```csharp
// String overload: compiles the regex with NonBacktracking + CultureInvariant
await Assert.That(httpLog)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Pattern(@"\brequest-id=[a-f0-9-]+", "request-id=<scrubbed>"));

// Regex overload: pass a pre-compiled Regex (recommended for hot paths)
private static readonly Regex SessionTokenPattern = new(
    @"\bsession=[A-Za-z0-9_-]+",
    RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

await Assert.That(httpLog)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Pattern(SessionTokenPattern, "session=<scrubbed>"));
```

Note that regex backreferences (`$1`, `${name}`) in the replacement token are **NOT** interpreted: the literal characters are emitted. This keeps the API predictable; if you need backreferences, the right tool is a custom scrubber (see [Custom scrubbers](#custom-scrubbers)).

### Composing multiple scrubbers

Chained `.WithScrubber(...)` calls compose left-to-right: the first scrubber receives the raw actual content; each subsequent scrubber receives the previous scrubber's output. All scrubbers in the chain share a single `SnapshotScrubberState` so recurring values keep stable tokens across the whole snapshot.

```csharp
await Assert.That(httpLog)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Pattern(@"\brequest-id=[a-f0-9-]+", "request-id=<scrubbed>"))
    .WithScrubber(Scrubbers.Default)  // applied AFTER the pattern scrubber
    .WithScrubber(Scrubbers.Pattern(@"\bclient-version=\d+\.\d+\.\d+", "client-version=<v>"));
```

When the same scrubber bundle is reused across many tests (the typical shape: `Scrubbers.Default` plus a handful of project-specific masks), assemble the bundle once with `Scrubbers.Combine(...)` and pass the single result to `.WithScrubber(...)`:

```csharp
private static readonly SnapshotScrubber FixturesScrubber = Scrubbers.Combine(
    Scrubbers.Default,
    Scrubbers.Pattern(@"\brequest-id=[a-f0-9-]+", "request-id=<scrubbed>"),
    Scrubbers.Pattern(@"\bclient-version=\d+\.\d+\.\d+", "client-version=<v>"));

[Test]
public async Task Reuses_the_bundle()
{
    await Assert.That(httpLog).MatchesSnapshot().WithScrubber(FixturesScrubber);
}
```

`Combine` returns an identity scrubber when the array is empty, the single element unchanged when the array has exactly one entry, and otherwise wraps a defensive copy of the array (so mutating it post-call does not affect the returned scrubber). All inner scrubbers share the same `SnapshotScrubberState` as when they would be chained directly.

### Custom scrubbers

`SnapshotScrubber` is a public abstract class. Derive from it to implement custom indexed or non-indexed scrubbing logic; the per-snapshot `SnapshotScrubberState` is supplied so custom scrubbers can keep stable token assignments for recurring values via `state.GetOrAssignIndex(kind, originalValue)`.

```csharp
public sealed class SessionIdScrubber : SnapshotScrubber
{
    private static readonly Regex Pattern = new(
        @"\bsession-[a-f0-9]{32}\b",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    public override string Apply(string input, SnapshotScrubberState state)
    {
        return Pattern.Replace(input, m =>
        {
            var idx = state.GetOrAssignIndex("session", m.Value);
            return $"<session:{idx}>";
        });
    }
}

// Use:
await Assert.That(serverLog)
    .MatchesSnapshot()
    .WithScrubber(new SessionIdScrubber());
```

Custom scrubbers must be deterministic for a given input, must not perform IO, must not capture cross-test state, and must not depend on time. The built-in scrubbers all use `RegexOptions.NonBacktracking | RegexOptions.CultureInvariant` (ReDoS-resistant); follow the same pattern for custom regexes.

## Failure diagnostics

### Mismatched baseline

```text
to match the snapshot baseline

Snapshot did not match the baseline.
  Expected: C:\src\MyApp.Tests\bin\Release\net10.0\Snapshots\PublicApiTests.Foo.expected.txt
  Actual:   C:\src\MyApp.Tests\bin\Release\net10.0\Snapshots\PublicApiTests.Foo.actual.txt

 public class MyApp.Foo
-public void OldMethod()
+public void RenamedMethod()
+public string NewProperty { get; }

To accept the change, rename the actual file over the expected file,
or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.
```

The diff uses unified-diff-style prefixes:

- ` ` (space): context line; same in both
- `-`: present in expected baseline only
- `+`: present in actual content only

For very large diffs (more than 20 differing lines), the output is truncated with a
count-summary footer indicating the total number of differing lines.

### No baseline

The first time a `MatchesSnapshot()` test runs, no baseline file exists yet. The assertion
fails with a clear "no baseline" diagnostic and writes the actual content to `.actual.txt`
so you can inspect it and rename to `.expected.txt`:

```text
to match the snapshot baseline

Snapshot baseline does not exist.
  Expected: C:\src\MyApp.Tests\bin\Release\net10.0\Snapshots\PublicApiTests.Foo.expected.txt
  Actual:   C:\src\MyApp.Tests\bin\Release\net10.0\Snapshots\PublicApiTests.Foo.actual.txt

Inspect the actual file and rename it to .expected.txt to accept it as the baseline,
or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.
```

## Accept-changes workflow

Three modes, in order of preference:

1. **IDE diff-and-merge.** Most IDEs (Rider, VS Code) detect side-by-side `.expected.txt`
   and `.actual.txt` files and offer a diff-and-merge view. This is the most ergonomic flow
   for reviewing a single change.
2. **Manual `cp`.** For users without IDE integration:
   ```bash
   cp Snapshots/MyTest.actual.txt Snapshots/MyTest.expected.txt
   ```
   Failure messages include full absolute paths so any external diff tool works.
3. **Bulk accept via env var.** When many snapshots changed (e.g. an intentional API surface
   refactor):
   ```bash
   SNAPSHOT_ACCEPT=1 dotnet test
   ```
   All mismatched snapshots are accepted automatically: the actual content overwrites the
   expected baseline, and the assertion passes (with `SnapshotMatchOutcome.Accepted`).

   **CI guard:** if the `CI` environment variable is also set to a truthy value (which all
   major hosted runners do: GitHub Actions, GitLab CI, Azure Pipelines, CircleCI, etc.),
   accept-mode is *refused* even if `SNAPSHOT_ACCEPT` is set. This makes it impossible for
   a stray pipeline configuration to silently accept baseline drift. See
   `SnapshotAcceptMode.IsActive` for the exact rule.

After step 1 or 2, you copy the change from your test binary's directory back into the
source `Snapshots/` folder; the next clean build picks up the new committed baseline. With
step 3 (bulk accept), the same applies: manually move the bin-directory `.expected.txt`
files into source.

## Cookbook: common patterns

**Pin a public API surface (the headline use case):**

```csharp
[Test]
public async Task Public_api_surface_matches_baseline()
{
    var assembly = typeof(MyLib.Foo).Assembly;
    var actual = ApiGenerator.GeneratePublicApi(assembly);

    await Assert.That(actual).MatchesSnapshot();
}
```

**Pin a rendered-text artefact (e.g., audit log, generated SQL, formatted report):**

```csharp
[Test]
public async Task Order_audit_log_matches()
{
    var order = new Order { /* ... */ };
    var auditLog = AuditLogRenderer.Render(order);

    await Assert.That(auditLog).MatchesSnapshot();
}
```

**Multiple snapshots in one test:**

```csharp
[Test]
public async Task Order_lifecycle_states_match()
{
    var order = new Order { /* ... */ };
    await Assert.That(order.RenderState()).MatchesSnapshot("Order.created");

    order.Confirm();
    await Assert.That(order.RenderState()).MatchesSnapshot("Order.confirmed");

    order.Ship();
    await Assert.That(order.RenderState()).MatchesSnapshot("Order.shipped");
}
```

**Cross-platform comparison (LF/CRLF differences not meaningful):**

```csharp
await Assert.That(actual).MatchesSnapshot(SnapshotOptions.NormalizedLineEndings);
```

**Custom comparison rules:**

```csharp
var options = SnapshotOptions.Default with
{
    LineEndingMode = SnapshotLineEndingMode.NormalizeToLF,
    TrailingWhitespace = SnapshotTrailingWhitespace.TrimTrailingPerLine,
    TrailingNewline = SnapshotTrailingNewline.Optional,
};

await Assert.That(actual).MatchesSnapshot(options);
```

**Use an explicit file path (e.g., shared snapshot across tests):**

```csharp
await Assert.That(actual).MatchesSnapshotFile("Snapshots/Shared/StatusTable.expected.txt");
```

**Parameterized tests with `[Arguments]`: per-row baselines:**

For parameterized tests, the default file resolver hashes the row's argument values (SHA-256, first 8 hex characters) and appends the hash to the snapshot file name, so each `[Arguments]` row gets its own baseline file instead of overwriting one another. Stringification uses `CultureInfo.InvariantCulture` for `IFormattable` values, so the same arguments produce the same hash across developer machines and CI regardless of current culture.

```csharp
[Test]
[Arguments("alpha", 200)]
[Arguments("beta", 404)]
public async Task Response_for_each_route_matches(string route, int statusCode)
{
    var rendered = RenderResponse(route, statusCode);
    await Assert.That(rendered).MatchesSnapshot();
}
```

Baselines committed under `Snapshots/` carry names of the form:

```text
{TestClassName}.{TestMethodName}.{ArgsHash8}.expected.txt
```

For example, the two rows above produce two distinct files such as
`Snapshots/Foo.Response_for_each_route_matches.7A3F1B2C.expected.txt` and
`Snapshots/Foo.Response_for_each_route_matches.D9E0A4B5.expected.txt`. The hash is computed from argument **values** in declaration order, so it is stable across runs and across machines (`InvariantCulture` is used for `IFormattable` types). Changing any value or reordering the values in the `[Arguments]` attribute changes the hash and requires renaming the committed baseline. Renaming the C# method parameters (without touching the values passed via `[Arguments]`) does **not** affect the hash. The argument types themselves do not appear in the file name; if two rows happen to stringify to the same byte sequence under `InvariantCulture`, they collide (a documented edge case for callers using custom types whose `ToString()` is not unique per value).

When a row's argument list is empty (no `[Arguments]` on the method, or `[Arguments()]` with no values), no hash is appended and the file name is `{TestClassName}.{TestMethodName}.expected.txt` as for non-parameterized tests.

**Reach for the curated `Scrubbers.Common` chain when the default three patterns are not enough:**

`Scrubbers.Default` covers `Guid` + `Iso8601Timestamp` + `UnixEpochMillis`. v0.4.0 adds `Scrubbers.Common`, a curated chain over five patterns: the three from `Default` plus `GuidN` (32-character `Guid.ToString("N")` format) and `ElapsedMs` (`42ms`, `42.5 ms`, etc.). Ordering is fixed at most-specific-first to avoid double-scrubbing.

```csharp
await Assert.That(actual)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Common);
```

Each new scrubber in `Common` (`GuidN`, `ElapsedMs`) is also exposed as a standalone property; use the standalone form when you want one new pattern without the rest.

**Combine `Scrubbers.Common` with project-specific scrubbers:**

The curated chain composes with custom scrubbers via `Scrubbers.Combine`. Place `Scrubbers.Common` first so domain scrubbers see already-normalized standard tokens; reverse the order if a domain scrubber must run before a standard pattern matches.

```csharp
private static readonly SnapshotScrubber FixturesScrubber = Scrubbers.Combine(
    Scrubbers.Common,
    new OtelTraceIdScrubber(),
    new NumericTokenScrubber("OrderId"));

await Assert.That(httpLog)
    .MatchesSnapshot()
    .WithScrubber(FixturesScrubber);
```

The composed bundle shares a single `SnapshotScrubberState`, so recurring values keep stable tokens across the whole pipeline regardless of which scrubber matched them first.

**Smart-diff suggestions in failure messages (v0.4.0):**

When `MatchesSnapshot()` fails, the failure message scans the rendered diff for known volatile patterns and recommends applicable built-in scrubbers. No configuration is required; the suggestions appear automatically between the diff and the accept-flow guidance:

```text
Snapshot did not match the baseline.
  Expected: /tmp/x.expected.txt
  Actual:   /tmp/x.actual.txt

- "Created 2026-05-07T13:45:30Z with id a1b2c3d4-..."
+ "Created 2026-05-07T13:48:11Z with id f9e8d7c6-..."

Suggestions: 4 differences match known volatile patterns.
  - 2 matches for GUID. Consider .WithScrubber(Scrubbers.Guid)
  - 2 matches for ISO 8601 timestamp. Consider .WithScrubber(Scrubbers.Iso8601Timestamp)
  Or use the curated chain: .WithScrubber(Scrubbers.Common)

To accept the change, rename the actual file over the expected file,
or set SNAPSHOT_ACCEPT=1 (in a non-CI shell) to accept automatically.
```

Wider diffs that match many patterns get a top-3-by-hit-count list plus a `... and N more pattern type(s) (N hits). Consider .WithScrubber(Scrubbers.Common)` rollup line, so the failure message stays scannable. Consumer-implemented custom scrubbers (subclasses of `SnapshotScrubber`) are not auto-detected by the suggestion engine; only the package's built-in patterns surface as recommendations.

**Render a typed object via `SnapshotRenderer<T>` (v0.4.0):**

For values that are not already strings, project them via a renderer. Two overloads cover the common shapes:

```csharp
// Inline projection via a delegate. Good for one-off canonicalization.
await Assert.That(myProto)
    .MatchesSnapshot(p => Formatter.Format(p))
    .WithScrubber(Scrubbers.Common);

// Reusable subclass for project-wide canonical renderers.
internal sealed class MyProtoRenderer : SnapshotRenderer<MyProto>
{
    public override string Render(MyProto value) => Formatter.Format(value);
}
// ...
await Assert.That(myProto).MatchesSnapshot(new MyProtoRenderer());
```

The renderer is invoked once per `CheckAsync` call; its output is fed through any chained scrubbers and then through the standard snapshot pipeline. If the renderer throws, the failure message includes the thrown exception name and message rather than crashing the test runner. If the renderer returns `null`, the assertion fails with `"renderer returned null content"`.

**Compose sibling family packages via the delegate overload (v0.4.0):**

A sibling family package (e.g. `LogAssertions.TUnit`) can publish snapshot renderers for its own types as static helper methods, without taking a reference on `SnapshotAssertions` or `SnapshotAssertions.TUnit`. Consumers compose at the test call site:

```csharp
// Inside LogAssertions.TUnit (no SnapshotAssertions reference required):
namespace LogAssertions.SnapshotRenderers;

public static class LogRecordRenderer
{
    public static string Render(LogRecord record) =>
        $"[{record.LogLevel}] {record.Category}: {record.MessageTemplate}";
}
```

```csharp
// Consumer test code (depends on both packages independently):
using LogAssertions.SnapshotRenderers;
using SnapshotAssertions;
using SnapshotAssertions.TUnit;

await Assert.That(logRecord)
    .MatchesSnapshot(LogRecordRenderer.Render)
    .WithScrubber(Scrubbers.Common);
```

This pattern keeps the family packages decoupled: each ships independently, with its own release cadence; renderers compose at the consumer's call site without requiring a bridge package or a transitive dependency. Sibling packages that want a configurable / stateful renderer can subclass `SnapshotRenderer<T>` instead; that path adds a reference on the framework-agnostic `SnapshotAssertions` core (no TUnit dep).

**Common renderer subclass shapes:**

Four renderer shapes cover the most-common projection targets. Each subclasses `SnapshotRenderer<T>` and exposes a static `Instance` field so callers re-use a single allocation across tests.

*Pattern 1: protobuf message via canonical JSON.* `Google.Protobuf.JsonFormatter` produces a deterministic field ordering across runs; reuse a static `Settings` instance to avoid per-call allocation. Requires the `Google.Protobuf` package.

```csharp
internal sealed class ProtobufJsonRenderer<T> : SnapshotRenderer<T>
    where T : IMessage
{
    public static readonly ProtobufJsonRenderer<T> Instance = new();

    private static readonly JsonFormatter Formatter =
        new(JsonFormatter.Settings.Default.WithIndentation("  "));

    public override string Render(T value) => Formatter.Format(value);
}
```

*Pattern 2: `XDocument` canonical XML.* `SaveOptions.None` preserves whitespace as authored; switch to `SaveOptions.DisableFormatting` if the snapshot should be insensitive to inter-element whitespace.

```csharp
internal sealed class XDocumentRenderer : SnapshotRenderer<XDocument>
{
    public static readonly XDocumentRenderer Instance = new();

    public override string Render(XDocument doc) => doc.ToString(SaveOptions.None);
}
```

*Pattern 3: domain value object, field by field.* Lay out the fields in a fixed order so the snapshot diff localizes per-field changes. Use `CultureInfo.InvariantCulture` for any `IFormattable` value to keep the baseline stable across developer machines and CI.

```csharp
internal sealed class OrderRenderer : SnapshotRenderer<Order>
{
    public static readonly OrderRenderer Instance = new();

    public override string Render(Order o) =>
        $"Id: {o.Id}\n" +
        $"Status: {o.Status}\n" +
        $"Total: {o.Total.ToString("F2", CultureInfo.InvariantCulture)}\n" +
        $"Items: [{string.Join(", ", o.Items.Select(i => i.Sku))}]";
}
```

*Pattern 4: domain timeline, one line per event.* For ordered collections, render one canonical line per element. The same shape works for log records, audit-trail rows, state-machine transitions, message envelopes.

```csharp
internal sealed class TimelineRenderer : SnapshotRenderer<IReadOnlyList<TimelineEvent>>
{
    public static readonly TimelineRenderer Instance = new();

    public override string Render(IReadOnlyList<TimelineEvent> events) =>
        string.Join("\n", events.Select(e =>
            $"{e.Timestamp.ToString("o", CultureInfo.InvariantCulture)} [{e.Kind}] {e.Message}"));
}
```

All four patterns work with both the renderer-typed overload (`MatchesSnapshot(OrderRenderer.Instance)`) and the inline factory (`MatchesSnapshot(Renderer.For<Order>(o => ...))`). Use the subclass form for project-wide renderers; the inline factory for one-off canonicalizations.

**Custom-scrubber cookbook (v0.4.0): subclass `SnapshotScrubber` for domain-specific volatile patterns:**

The two new built-in scrubbers (`GuidN`, `ElapsedMs`) cover universally-common patterns. Domain-specific patterns (OpenTelemetry trace IDs, gRPC `RpcException` details, environment-specific paths, monotonic counters with project-specific prefixes) are not shipped as built-ins; consumers implement them by subclassing the already-public `SnapshotScrubber` base class (shipped in v0.2.0). The cookbook below provides four worked examples covering the common subclass shapes.

*Example 1: `OtelTraceIdScrubber`* (context-anchored partial-match replacement). W3C Trace Context payloads are bare lowercase hex with no structural feature distinguishing them from other 32-char hex (canonical GUIDs in N-format, SHA-256 prefixes, etc.). Anchor on the explicit context emitters use in structured logs (`trace_id=`, `traceId=`, `traceparent=00-`), and scrub only the captured hex group; the context prefix is preserved literally in the output.

```csharp
internal sealed partial class OtelTraceIdScrubber : SnapshotScrubber
{
    private static readonly Regex Pattern = OtelTraceIdRegex();

    public override string Apply(string input, SnapshotScrubberState state)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(state);
        return Pattern.Replace(input, m =>
        {
            var key = m.Groups[1].Value.ToLowerInvariant();
            var idx = state.GetOrAssignIndex("trace", key);
            var prefix = m.Value[..(m.Groups[1].Index - m.Index)];
            return prefix + string.Create(CultureInfo.InvariantCulture, $"<trace:{idx}>");
        });
    }

    [GeneratedRegex(
        @"\b(?:trace[_-]?id\s*[:=]\s*|traceparent=00-)([0-9a-f]{32})\b",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex OtelTraceIdRegex();
}
```

*Example 2: `EphemeralPathScrubber`* (cross-OS pattern matching). Match Windows temp paths (`C:\Users\<user>\AppData\Local\Temp\...`), POSIX `/tmp/...` and `/var/tmp/...`, macOS `/var/folders/...`. Same indexed-token shape as the built-ins.

```csharp
internal sealed partial class EphemeralPathScrubber : SnapshotScrubber
{
    private static readonly Regex Pattern = EphemeralPathRegex();

    public override string Apply(string input, SnapshotScrubberState state)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(state);
        return Pattern.Replace(input, m =>
        {
            var idx = state.GetOrAssignIndex("temp-path", m.Value);
            return string.Create(CultureInfo.InvariantCulture, $"<temp-path:{idx}>");
        });
    }

    [GeneratedRegex(
        @"(?:[A-Za-z]:\\Users\\[^\\]+\\AppData\\Local\\Temp\\[^\s""']+|/tmp/[^\s""']+|/var/tmp/[^\s""']+|/var/folders/[^\s""']+)",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex EphemeralPathRegex();
}
```

*Example 3: `PortScrubber`* (simplest indexed-token reference). A 2-to-5-digit number preceded by `:`. The trailing `\b` prevents matching the prefix of a longer numeric token.

```csharp
internal sealed partial class PortScrubber : SnapshotScrubber
{
    private static readonly Regex Pattern = PortRegex();

    public override string Apply(string input, SnapshotScrubberState state)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(state);
        return Pattern.Replace(input, m =>
        {
            var idx = state.GetOrAssignIndex("port", m.Value);
            return string.Create(CultureInfo.InvariantCulture, $"<port:{idx}>");
        });
    }

    [GeneratedRegex(@":\d{2,5}\b", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
    private static partial Regex PortRegex();
}
```

*Example 4 (parameterised variant): `NumericTokenScrubber`* (constructor-captured prefix, dynamic kind name). Useful for monotonic counters like `CycleId=42`, `SampleId=7`, `OrderId=1001`. The prefix is captured in a constructor field so multiple instances with different prefixes do not share an index space.

```csharp
internal sealed class NumericTokenScrubber : SnapshotScrubber
{
    private readonly Regex _pattern;
    private readonly string _kind;

    public NumericTokenScrubber(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var escaped = Regex.Escape(prefix);
        _pattern = new Regex(
            $@"\b{escaped}[=:\s]+(\d+)\b",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        _kind = prefix.ToLowerInvariant();
    }

    public override string Apply(string input, SnapshotScrubberState state)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(state);
        return _pattern.Replace(input, m =>
        {
            var idx = state.GetOrAssignIndex(_kind, m.Groups[1].Value);
            var prefix = m.Value[..(m.Groups[1].Index - m.Index)];
            return prefix + string.Create(CultureInfo.InvariantCulture, $"<{_kind}:{idx}>");
        });
    }
}
```

Two `NumericTokenScrubber` instances with prefixes that differ only in case (e.g. `"CycleId"` and `"cycleid"`) share kind name and therefore share index space because of the `ToLowerInvariant()` normalisation; that matches the `Guid` / `GuidN` shared-index pattern in the built-ins. To get distinct index spaces, vary the prefix substantively (different word, not different casing).

Common subclass mechanics across all four examples: use `state.GetOrAssignIndex(kind, value)` for indexed-token state; apply `RegexOptions.NonBacktracking | RegexOptions.CultureInvariant`; word-boundary anchor with `\b` since lookarounds are not available in `NonBacktracking`; produce token strings as `<kind:N>`. To share an index space with a built-in scrubber (e.g. give your custom variant the same `"guid"` kind name as `Scrubbers.Guid`), reuse the kind name verbatim.

## Comparison with Verify

| Capability | `SnapshotAssertions.TUnit` | Verify |
|---|---|---|
| Text snapshot (string → file) | ✅ | ✅ |
| Object-graph snapshot (any object → file) | ❌ | ✅ |
| Automatic scrubbing of dynamic content (Guids, dates, IPs) | ⚠️ partial: opt-in via `WithScrubber()` for GUIDs / ISO 8601 / Unix-epoch-millis / custom regex; no IPs or auto-detection | ✅ |
| IDE-integrated diff display | ❌ (relies on file paths in failure message) | ✅ |
| `Microsoft.CodeCoverage` Linux compatibility | ✅ | ❌ ([TUnit#4149](https://github.com/thomhurst/TUnit/discussions/4149)) |
| AOT-compatible | ✅ | ⚠️ (some scenarios) |
| TUnit-native via `[AssertionExtension]` | ✅ | ✅ via `Verify.TUnit` |
| Coexists in the same project | ✅ | ✅ |

**When to use which:** if you only need text-snapshot assertions and run coverage on Linux,
prefer `SnapshotAssertions.TUnit`. The opt-in scrubbers (GUIDs, ISO 8601 timestamps, Unix-epoch-millis,
custom regex) cover the common volatile-value cases via `WithScrubber()`: see [Scrubbers](#scrubbers-volatile-value-handling).
If you need object-graph diffing or IDE-integrated diff display, use Verify (and accept the
Linux coverage workaround). For auto-detection of arbitrary dynamic content (e.g. IP addresses,
hostnames, paths) without writing a regex, Verify is also the better fit. The two libraries
do not depend on each other and can coexist in the same project.

### Migrating from Verify.TUnit

Common Verify.TUnit calls and their `SnapshotAssertions.TUnit` equivalents:

| Verify.TUnit | SnapshotAssertions.TUnit |
| --- | --- |
| `await Verifier.Verify(actual)` | `await Assert.That(actual).MatchesSnapshot()` |
| `Verifier.UseDirectory("Snapshots")` | Default — `SnapshotAssertions.TUnit` writes to `Snapshots/` next to the test class automatically; no setup needed. Use `MatchesSnapshotFile("custom/path.expected.txt")` per-call to override the default. |
| `Verifier.AddScrubber(s => ...)` | `await Assert.That(actual).MatchesSnapshot().WithScrubber(Scrubbers.Pattern(regex, "TOKEN"))` |
| Auto-accept via `.received` rename | `SNAPSHOT_ACCEPT=1 dotnet test` (local; refused if `CI` is set) |

`Verifier.Verify(actual).IgnoreMember("Id")` has no one-to-one equivalent because Verify ignores members at the object-graph level (pre-serialization) while `SnapshotAssertions` scrubbers run against the rendered string (post-serialization). Two practical replacements:

1. Project the value through a custom `SnapshotRenderer<T>` that omits the field:

```csharp
internal sealed class OrderRendererWithoutId : SnapshotRenderer<Order>
{
    public static readonly OrderRendererWithoutId Instance = new();

    public override string Render(Order o) =>
        $"Status: {o.Status}\nTotal: {o.Total.ToString("F2", CultureInfo.InvariantCulture)}";
}
```

2. Pre-serialize the value, then scrub the serialized form with a regex pattern matching the field's rendered shape:

```csharp
var json = JsonSerializer.Serialize(order, MyJsonContext.Default.Order);

await Assert.That(json)
    .MatchesSnapshot()
    .WithScrubber(Scrubbers.Pattern(@"""Id"":\s*\d+", @"""Id"": <scrubbed>"));
```

The serialization step is explicit because `MatchesSnapshot()` operates on strings; the regex assumes the JSON-serialized shape of the field. For a one-chain variant, pass the serializer as an inline renderer:

```csharp
await Assert.That(order)
    .MatchesSnapshot(o => JsonSerializer.Serialize(o, MyJsonContext.Default.Order))
    .WithScrubber(Scrubbers.Pattern(@"""Id"":\s*\d+", @"""Id"": <scrubbed>"));
```

Pattern (1) is cleaner when several tests need the same omission; pattern (2) is the one-liner for ad-hoc cases.

Key differences beyond the call-site mapping:

- Composition first. Scrubbers and renderers compose via `Scrubbers.Combine` and `.WithScrubber(...)` chains rather than registry mutation. No global state.
- Baselines live next to the test class by default; no `UseDirectory` setup unless you want to override.
- AOT-clean by design. There is no reflection-based default renderer. Custom types require an explicit `SnapshotRenderer<T>` or a `Renderer.For<T>(Func<T, string>)` inline factory.

For Verify features not listed here, open a [Discussion](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/discussions) with a concrete use case and either a mapping will be added or the gap will be documented.

## Troubleshooting

### `MatchesSnapshot()` throws `InvalidOperationException` about no test context

The default-path resolver reads `TestContext.Current` to derive the test class and method
names. `TestContext.Current` is null outside a `[Test]` method (e.g. in a `[Before(Test)]`
hook running before the test method's context is established, or when the assertion is
called from a non-TUnit harness).

**Fix:** pass an explicit name or path:

```csharp
await Assert.That(actual).MatchesSnapshot("MyExplicitName");
// or:
await Assert.That(actual).MatchesSnapshotFile("/some/path.expected.txt");
```

### Snapshots aren't found when the test runs

Symptom: every test reports "Snapshot baseline does not exist" with an `.actual.txt` file
written next to where the expected file would be.

**Most likely cause:** your `.csproj` is not copying `Snapshots/*.expected.txt` to the test
binary's output directory. Add the `<None Include>` from
[Project setup](#project-setup-csproj-wiring).

### Tests pass on Windows but fail on Linux (CRLF mismatch)

Symptom: the diff shows trailing `\r` characters on every line.

**Cause:** the committed `.expected.txt` was saved with CRLF line endings (a Windows
default), but the test on Linux produces LF.

**Fix one (recommended):** add `*.expected.txt text eol=lf` to `.gitattributes` so committed
baselines are always LF, then re-commit the file (Git will convert it).

**Fix two:** use the cross-platform preset on the call site:

```csharp
await Assert.That(actual).MatchesSnapshot(SnapshotOptions.NormalizedLineEndings);
```

### Bootstrapping baselines for a fresh test

The first run of a new `MatchesSnapshot()` test fails with "no baseline" because there's no
committed `.expected.txt` yet. Two ways to bootstrap:

1. Run once. The failure writes `.actual.txt`. Rename it to `.expected.txt`, copy back to your
   source `Snapshots/` folder, commit.
2. Run with `SNAPSHOT_ACCEPT=1 dotnet test` (locally, never in CI). The `.actual.txt` content
   is written directly over `.expected.txt`. Then move the file from the test binary's
   directory back into your source `Snapshots/` folder.

### CI accidentally accepts a snapshot

This shouldn't happen: accept-mode is refused if `CI` is set, regardless of
`SNAPSHOT_ACCEPT`. If you observe it happening anyway, check:

- Did your CI runner set `CI=true` (or `CI=1` / `CI=yes`)? Verify with a `printenv CI` step.
- Are you running an older version of the package? `SnapshotAcceptMode.IsActive(string?, string?)`
  with explicit values is the deterministic test surface; the no-arg overload reads the live
  environment.

## Modern .NET 10+ practices on display

Each row points at a fact verifiable from the source code or build output.

| Practice | Where in this package |
|---|---|
| **AOT-compatible** | `IsAotCompatible=true` in both csprojs. AOT analyzers run during `dotnet build`. No `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` annotations. |
| **Trimmable** | `IsTrimmable=true`. Tiny public surface; nothing to annotate. |
| **AOT-publish CI gate** | `dotnet publish -r linux-x64 --aot` against the smoke-test consumer in `ci.yml`. Strongest possible AOT guarantee: not just "AOT-compatible by analyzer," but "actually publishes to native code without warnings." |
| **No reflection in the assertion path** | The assertion only does file I/O, string comparison, and rendering. No `MethodBase.Invoke`, no `Activator.CreateInstance(Type)`, no runtime type discovery. |
| **CancellationToken throughout** | Every async public API accepts `CancellationToken ct = default`. |
| **Async file I/O end-to-end** | `File.ReadAllTextAsync`, `File.WriteAllTextAsync`. No sync-over-async. |
| **C# 14 / `LangVersion=14.0`** | File-scoped namespaces, record types, primary constructors, required members, nullable reference types enforced. |
| **Deterministic builds + Source Link + SBOM + reproducible restore** | `Deterministic=true`, embedded Source Link, `Microsoft.Sbom.Targets` SBOM generation, lock files + `--locked-mode` restore on CI. |
| **Trusted Publishing (OIDC) for NuGet** | No long-lived API keys in CI secrets. |
| **5 Roslyn analyzer packs at full strength** | Meziantou.Analyzer, SonarAnalyzer.CSharp, Roslynator.Analyzers, Microsoft.VisualStudio.Threading.Analyzers, DotNetProjectFile.Analyzers. `TreatWarningsAsErrors=true`. No suppressions without per-call justification comment. |
| **`StringComparison.Ordinal` everywhere** | Per Meziantou MA0006. No silent culture defaults. |
| **External-consumer smoke test in CI** | Packed `.nupkg`, deliberately-different namespace, exercises every public entry point, AOT-publishes. |
| **Coverage gates: 90% line / 80% branch** | Hard CI gate; build fails if either drops below threshold. |

## Design notes

### `MatchesSnapshot()` is post-mortem, not interactive

The assertion compares the actual content against the committed baseline at evaluation time
and either passes or throws. It does not prompt, open a diff window, or wait for input.
Interactive flows (IDE diff-and-merge view) are provided by the IDE based on the side-by-side
`.expected.txt` / `.actual.txt` files the assertion writes; the assertion itself stays
machine-evaluable so the same code path runs identically locally and in CI.

### Accept-mode lives in `SnapshotEvaluator`, not in the assertion

The decision "should I overwrite the baseline?" is made inside the evaluator after the
comparer reports a mismatch. This keeps the TUnit adapter thin and the accept-mode logic
independently testable: `SnapshotAcceptMode.IsActive(string?, string?)` is pure (no env var
reads), so its full truth table is verified by 7 unit tests.

### `CI=true` guard is intentionally unconditional

`SnapshotAcceptMode.IsActive` returns `false` whenever `CI` is truthy, regardless of
`SNAPSHOT_ACCEPT`. There's no way to override the guard from inside the package (no
"force" flag, no per-test attribute). This is deliberate: the failure mode of an accidental
override is silent baseline drift in committed history, which is exactly what the snapshot
test is meant to prevent.

### Default-name resolution uses simple class name, not full namespace

`{TestClassName}.{TestMethodName}` rather than `{Namespace}.{TestClassName}.{TestMethodName}`.
The simpler form keeps file names short and avoids cross-platform path-length surprises.
If two test classes share a name across namespaces, pass an explicit name to disambiguate.

### Why no `[CallerFilePath]`-based source-relative paths

Some snapshot libraries use `[CallerFilePath]` to derive paths relative to the test source
file rather than the binary directory. `SnapshotAssertions.TUnit` doesn't, because:

- `[CallerFilePath]` doesn't propagate cleanly through TUnit's source-generated assertion entry methods: the values would all point at the generated extension, not the user's call site.
- The binary-directory convention is well-trodden (Verify uses it for `Snapshots/`-folder layouts under `<None Include CopyToOutputDirectory>`) and integrates cleanly with `<None Include>` csproj wiring.

The trade-off is that consumers must wire `<None Include>` in their csproj. The README
documents this prominently.

### Strict defaults

`SnapshotOptions.Default` preserves everything: line endings, BOM, trailing whitespace,
trailing newline. Cross-platform false positives are real but should be opted into via
`SnapshotOptions.NormalizedLineEndings` rather than silently normalized away. Same convention
as the family-wide explicit-`StringComparison` rule.

## Stability intent (pre-1.0)

The 0.x series may include breaking changes on minor-version bumps. Concretely:

- The **public types and members** documented above (`SnapshotAssertion`, the
  `MatchesSnapshot*` extensions, `SnapshotOptions` and the four enum types,
  `SnapshotComparer`, `SnapshotEvaluator`, `SnapshotFileResolver`, `SnapshotAcceptMode`,
  `LineDiffRenderer`, `SnapshotResult`, `SnapshotMatchOutcome`, `SnapshotPaths`,
  `SnapshotException`) are **intended-stable**. A rename or removal would require a major
  version bump even pre-1.0.
- The **exact text format of the line-based diff** rendered by `LineDiffRenderer` is **not
  stable**. The format may gain extra detail or change in any release. Pin the *outcome* of
  a `MatchesSnapshot()` assertion (pass / fail), not the exact failure message text.
- The **distinction between `SnapshotTrailingNewline.Optional` and `Forbidden`** is currently
  cosmetic: both produce the same equality outcome. Future versions may add a stricter
  check for `Forbidden` that fails when either side has a trailing newline.

`PackageValidationBaselineVersion` tracks the previous shipped version (pinned to 0.1.0 in 0.2.0; bumped to 0.2.0 in 0.3.0); once pinned, any breaking change to the listed surface fails the package-validation build.

## Limitations and future work

### Resolved

- ✅ **0.1.1 housekeeping**: folded into 0.2.0 (dependency refresh, `PackageValidationBaselineVersion=0.1.0`, CONVENTIONS.md v0.2). Shipped in v0.2.0.
- ✅ **Pattern-based scrubbing**: originally targeted for 0.3.0 under `MatchesSnapshotScrubbed(IScrubber)`. Pulled forward to 0.2.0 with a different shape: `.WithScrubber(SnapshotScrubber)` chain method, `Scrubbers` static factory, and indexed-token format for stable recurring-value handling. See [Scrubbers (volatile value handling)](#scrubbers-volatile-value-handling).
- ✅ **Reusable scrubber bundles**: `Scrubbers.Combine(params SnapshotScrubber[])` factory shipped in v0.3.0. Lets a project assemble `Scrubbers.Default + custom-patterns` once as a `static readonly` field and pass that single value to `.WithScrubber(...)` on every assertion rather than re-chaining the same bundle per test.
- ✅ **Snapshot.Render namespace reservation**: `SnapshotAssertions.Render` reserved in v0.3.0 as the family convention for sibling-package text renderers (consumed via `using SnapshotAssertions.Render;`). No public surface ships under it from this package today; the namespace exists to keep sibling renderer entry points discoverable through a single `using` directive.
- ✅ **Parameterized-test baselines**: `MatchesSnapshot()` already routes `[Arguments]`-row values through `SnapshotFileResolver` and produces per-row baseline files (`{TestClass}.{TestMethod}.{ArgsHash8}.expected.txt`). The v0.3.0 cookbook documents the file-name convention, the `InvariantCulture`-stable hash contract, and the edge cases (same stringified bytes collide; argument-order change requires baseline rename).

### Confirmed roadmap

- **0.4.0+**: Dedicated framework-agnostic test project for the `SnapshotAssertions` core (no TUnit reference), enforcing the BCL-only public-API contract end-to-end at build time. Internal architectural work; no public-surface impact.
- **Demand-driven (0.4.0+)**: JSON-aware snapshot comparison (`MatchesJsonSnapshot()`) with property-order / array-order / ignored-properties options. Originally targeted for 0.3.0; deferred to keep the `JsonSnapshotOptions` shape from being designed against a speculative spec. The bundled `Scrubbers` plus sorted-key serialization handles the typical JSON-snapshot case today. Open a GitHub issue with a concrete example if the current path falls short.
- **Demand-driven**: Verify interop helpers (`ToVerifyString()` adapters), shipped only on request via a GitHub issue with a concrete use case; coexistence is supported today without them.

Out of scope (intentionally: use Verify instead):

- Object-graph diffing
- Image / binary diffing
- IDE plugins
- Cross-process snapshot comparison

## Family compatibility

The six assertion-family packages: `LogAssertions.TUnit`, `TimeAssertions.TUnit`, `SnapshotAssertions.TUnit`, `MathAssertions.TUnit`, `JsonAssertions.TUnit`, and `SseAssertions.TUnit`: release independently and target the same .NET TFM at any moment (LTS-anchored, multi-target during STS support windows; see the [TFM policy in CONVENTIONS.md](CONVENTIONS.md#tfm-policy) for the rotation schedule). **Mix versions freely.** Each package ships under SemVer with `EnablePackageValidation` strict-mode ApiCompat against its previous baseline, so binary breaks within a version line are caught at pack time.

For per-package release notes:
- [LogAssertions.TUnit CHANGELOG](https://github.com/JohnVerheij/LogAssertions.TUnit/blob/main/CHANGELOG.md)
- [TimeAssertions.TUnit CHANGELOG](https://github.com/JohnVerheij/TimeAssertions.TUnit/blob/main/CHANGELOG.md)
- [SnapshotAssertions.TUnit CHANGELOG](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/blob/main/CHANGELOG.md)
- [MathAssertions.TUnit CHANGELOG](https://github.com/JohnVerheij/MathAssertions.TUnit/blob/main/CHANGELOG.md)
- [JsonAssertions.TUnit CHANGELOG](https://github.com/JohnVerheij/JsonAssertions.TUnit/blob/main/CHANGELOG.md)
- [SseAssertions.TUnit CHANGELOG](https://github.com/JohnVerheij/SseAssertions.TUnit/blob/main/CHANGELOG.md)

## Pair with

- **[`LogAssertions.TUnit`](https://www.nuget.org/packages/LogAssertions.TUnit/)**: fluent
  log assertions over `Microsoft.Extensions.Logging.Testing.FakeLogCollector`. Use
  `MatchesSnapshot()` to pin the rendered output of `LogAssertions`'s `LogAssertionRendering`
  in integration tests.
- **[`TimeAssertions.TUnit`](https://www.nuget.org/packages/TimeAssertions.TUnit/)**:
  assertion-level timing budgets via `.And.WithinTimeBudget(...)` plus `FakeTimeProvider`
  state checks. Compose with `MatchesSnapshot()` when you want both content stability and a
  bounded execution-time guarantee in the same assertion chain.
- **[`MathAssertions.TUnit`](https://www.nuget.org/packages/MathAssertions.TUnit/)**:
  tolerance-aware numeric and geometric assertions over the BCL and `System.Numerics` types.
  Mixes freely with snapshot assertions in multi-assertion tests for numerically-rich
  rendered artefacts.
- **[`JsonAssertions.TUnit`](https://www.nuget.org/packages/JsonAssertions.TUnit/)**:
  fluent JSON assertions over `System.Text.Json`, HTTP response bodies (including RFC 7807
  ProblemDetails), and source-generated `JsonSerializerContext` registration.
- **[`SseAssertions.TUnit`](https://www.nuget.org/packages/SseAssertions.TUnit/)**:
  Server-Sent Events (SSE) wire-format assertions: event-count, field shape (`event:`,
  `data:`, `id:`, `retry:`), and stream content validation.

## Contributing

Issues and pull requests welcome. Before opening a PR:

- Run `dotnet build` and `dotnet test` locally; the CI pipeline enforces the same quality bar (zero warnings as errors, 90% line / 90% branch coverage minimum).
- Match the existing code style (`.editorconfig` is authoritative; `dotnet format` covers formatting).
- For new assertions, include a test for both the happy path and a representative failure case so the failure-message rendering is verified.

For larger ideas (new entry points, breaking changes, cross-cutting refactors), open a [Discussion](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/discussions) first to align on direction before investing implementation time.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full PR review checklist and API design principles, and [CONVENTIONS.md](CONVENTIONS.md) for the family-wide code conventions shared across `LogAssertions.TUnit`, `SnapshotAssertions.TUnit`, `TimeAssertions.TUnit`, `MathAssertions.TUnit`, `JsonAssertions.TUnit`, and `SseAssertions.TUnit`.

## License

[MIT](LICENSE). Copyright (c) 2026 John Verheij.
