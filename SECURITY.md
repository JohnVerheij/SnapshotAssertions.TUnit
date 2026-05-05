# Security Policy

## Supported versions

Only the latest published version of `SnapshotAssertions.TUnit` receives fixes. Earlier versions are not supported.

| Version | Supported |
|---------|-----------|
| latest  | ✅        |
| older   | ❌        |

## Reporting a vulnerability

If you discover a security vulnerability, **please do not open a public GitHub issue.** Instead, report it privately via [GitHub's private security reporting](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/security/advisories/new).

Reports are acknowledged within seven days. After a fix is prepared, a coordinated disclosure timeline is agreed with the reporter before public release.

## Scope

This package is a TUnit-targeting test-only library. Realistic attack surface is small: it reads a baseline file from disk and compares it against a string supplied by the test, then renders a line-based diff into the assertion failure message. Issues that may qualify:

- Path traversal via a crafted snapshot name or explicit file path
- Unbounded memory or CPU consumption from a crafted baseline file
- Information disclosure through assertion failure messages that escapes intended scope
- Supply-chain concerns about the package itself

Issues that do not qualify:

- Bugs in dependent packages (TUnit, PublicApiGenerator) — report those upstream
- Issues in test-runner integration that are TUnit-side
