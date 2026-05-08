# Security policy

SnapshotAssertions / SnapshotAssertions.TUnit is a personal-name open-source project distributed under the [MIT license](LICENSE). Although the package is intended for use **only in test projects** (see the scope blockquote on every README), security reports are still welcome and treated as a priority.

This document is the project's coordinated-vulnerability-disclosure (CVD) policy. The [Cyber Resilience Act (Regulation (EU) 2024/2847)](https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX%3A32024R2847) imposes vulnerability-handling and reporting obligations on Manufacturers of products with digital elements; personal-OSS distributed outside the course of commercial activity is excluded from CRA scope and the policy here is published anyway as a hygiene measure for downstream consumers.

## Reporting a vulnerability

**Please do NOT open a public issue for security vulnerabilities.** Use one of the private channels below.

### Preferred: GitHub private vulnerability reporting

1. Navigate to the repository's [Security tab](https://github.com/JohnVerheij/SnapshotAssertions.TUnit/security)
2. Click **Report a vulnerability**
3. Fill in the form with reproduction steps, affected version(s), and any proof-of-concept code

Reports submitted this way are visible only to repository maintainers and are end-to-end encrypted by GitHub. The reporter is notified of progress through the same channel.

### Alternative: open a contact-request issue

If GitHub's private reporting form is unavailable in your account (e.g. organisation-level restrictions), open a regular issue with the title `Security: contact request` and **no exploit details in the body** — just enough to indicate you have a security report to file. The maintainer will move the conversation to a private channel from there.

(Drafting a GitHub Security Advisory directly is restricted to repository admins and security managers, so it is not a viable fallback for external reporters.)

## What to include in a report

A useful report typically includes:

- **Affected version(s)** — exact NuGet package version where the issue is observed
- **Severity assessment** — your view of the impact (CVSS 3.1 or 4.0 vector if you have one, otherwise prose)
- **Reproduction steps** — the smallest test project / call sequence that triggers the issue
- **Suggested fix or mitigation** if you have one

Reports without these are still welcome — maintainers will work with you to fill the gaps.

## Response targets

Personal-OSS maintained outside working hours; the targets below are best-effort, not contractual:

| Stage | Target |
|---|---|
| Acknowledgement of report | Within 5 business days |
| Initial triage (severity, affected versions) | Within 14 calendar days |
| Fix shipped to nuget.org or coordinated disclosure plan | Within 90 calendar days from triage |

A **coordinated disclosure** (public CVE filing + advisory + patched release) is the default outcome unless the reporter requests otherwise. Credit is given to the reporter in the published advisory and CHANGELOG entry; reporters who prefer to remain anonymous are accommodated.

## Supported versions

| Version line | Status | Receives security fixes |
|---|---|---|
| `0.2.x` (SnapshotAssertions.TUnit) | **Current** | ✅ Yes |
| `0.1.x` | Previous stable | ✅ Yes (security fixes only; no new features) |
| `< 0.1.0` | Pre-stable | ❌ No |

This table is updated alongside each release that bumps the current line. Coverage of older lines for security-only fixes follows the [.NET LTS / STS rotation](CONVENTIONS.md#tfm-policy) — when the package's TFM changes at a major-version boundary, security fixes for the previous line continue to ship for one minor cycle.

## Scope

In scope:

- The shipped `SnapshotAssertions` and `SnapshotAssertions.TUnit` NuGet packages
- The build pipeline (release workflows, signing, NuGet publishing chain) and supply-chain integrity of the packages
- The shipped SBOMs (SPDX 3.0 in nupkg, CycloneDX 1.6 sibling artifact)
- The SLSA build-provenance attestations and Sigstore-signed SBOM attestations
- Realistic in-product attack surface examples:
  - Path traversal via a crafted snapshot name or explicit file path
  - Unbounded memory or CPU consumption from a crafted baseline file
  - Information disclosure through assertion failure messages that escapes intended scope
  - Regex-pattern denial of service through a custom `Scrubbers.Pattern(...)` consumer (note: the built-ins use `RegexOptions.NonBacktracking`)

Out of scope (please report upstream instead):

- Vulnerabilities in **direct dependencies** (`TUnit.*`, `PublicApiGenerator`, etc.) — report to the relevant upstream maintainer; we will pull in their fix once released
- Vulnerabilities in **the .NET runtime / SDK** itself — report to [Microsoft Security Response Center](https://msrc.microsoft.com/)
- Issues affecting **production code** that incorrectly references this package — see the "Test projects only" scope blockquote on the package README; production use is unsupported by design

## What this project does NOT have

To set expectations correctly:

- **No bug bounty.** Reports are accepted gratefully but no monetary reward is offered.
- **No SLA.** Response targets above are best-effort.
- **No commercial support contract.** This is personal-OSS; for commercial-grade support, fork the package and add the support layer yourself, or sponsor an alternative testing library that ships with one.

## Verifying a published release

Every release shipped to nuget.org and GitHub Releases carries cryptographic supply-chain attestations stored in GitHub's public transparency log. A security-conscious consumer can verify the chain end-to-end before adopting:

Replace `<package>` (one of `SnapshotAssertions` or `SnapshotAssertions.TUnit`) and `<version>` (e.g. `0.2.0`) with the artifact you downloaded:

```bash
# Verify SLSA v1.0 build provenance: was this nupkg built from this audited source tree?
gh attestation verify ./<package>.<version>.nupkg --repo JohnVerheij/SnapshotAssertions.TUnit

# Verify the CycloneDX SBOM: is this BOM the authentic SBOM for that nupkg?
gh attestation verify ./<package>.<version>.nupkg \
    --repo JohnVerheij/SnapshotAssertions.TUnit --predicate-type https://cyclonedx.org/bom
```

Both attestations are signed via [Sigstore](https://www.sigstore.dev/) keyless signing — there is no long-lived signing key for an attacker to compromise; verification proves the artifact came from this exact GitHub repo at the exact commit and workflow that built it.

| Layer | Mechanism | Format / standard |
|---|---|---|
| Source code → build environment | GitHub Actions workflow with pinned action SHAs | — |
| Build → nupkg artifact | `actions/attest-build-provenance@v2` | [SLSA v1.0 Provenance](https://slsa.dev/spec/v1.0/provenance) |
| Build → SBOM (in-package) | `Microsoft.Sbom.Targets` at pack time | SPDX 3.0 (in `_manifest/spdx_3.0/`) |
| Build → SBOM (sibling) | CycloneDX dotnet tool + `actions/attest@v4` | CycloneDX 1.6 + Sigstore signature |
| Build → vulnerability disclosure | VEX (Vulnerability Exploitability eXchange) | [OpenVEX v0.2.0](https://github.com/openvex/spec) sibling release artifact |
| Artifact → nuget.org | NuGet OIDC Trusted Publishing | — |
| Git commits + tags | SSH-signed via 1Password | — |

This stack matches what most CRA-bound EU Manufacturers will be required to ship from December 2027. Personal-OSS distributed outside the course of commercial activity is excluded from CRA scope; shipping the stack anyway is a hygiene choice.

## Acknowledgements

Thanks to all reporters who help keep the package family secure. Public credit is given on each advisory unless anonymity is requested.
