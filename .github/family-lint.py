#!/usr/bin/env python3
"""Family lint gate (CI-safe: no sensitive terms). Run from a repo root with no
args; lints CHANGELOG.md (Keep a Changelog + family conventions) and every
src/**/*.csproj <PackageTags> (two-variant order). Exit 1 on any violation."""
import re, sys, glob, os

ALLOWED_H3 = {"Added", "Changed", "Deprecated", "Removed", "Fixed", "Security", "BREAKING"}
ORDER = ["Added", "Changed", "Deprecated", "Removed", "Fixed", "Security"]
VER_RE = re.compile(r"^## \[(\d+\.\d+\.\d+)\] - \d{4}-\d{2}-\d{2}: \S.*$")
VER_LOOSE = re.compile(r"^## \[(\d+\.\d+\.\d+)\]")
FOOT_RE = re.compile(r"^\[(\d+\.\d+\.\d+)\]:\s+\S")
TAGS_RE = re.compile(r"<PackageTags>(.*?)</PackageTags>")

def lint_changelog(path):
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
    v, header_versions, footer_versions = [], [], []
    if not any(l.strip() == "## [Unreleased]" for l in lines):
        v.append("missing '## [Unreleased]' section")
    if not any(l.strip().lower().startswith("[unreleased]:") for l in lines):
        v.append("missing '[unreleased]:' footer link")
    cur, seen = None, []
    for i, l in enumerate(lines, 1):
        if l.startswith("## ["):
            m = VER_LOOSE.match(l)
            if m and l.strip() != "## [Unreleased]":
                header_versions.append(m.group(1))
                if not VER_RE.match(l):
                    v.append(f"L{i}: version header not '## [x.y.z] - YYYY-MM-DD: summary': {l.strip()!r}")
                cur, seen = m.group(1), []
        elif l.startswith("#### "):
            v.append(f"L{i}: sub-header deeper than '###' not allowed: {l.strip()!r}")
        elif l.startswith("### "):
            name = l[4:].strip()
            if name not in ALLOWED_H3:
                v.append(f"L{i}: non-standard section '### {name}'")
            elif name in ORDER:
                idx = ORDER.index(name)
                if seen and idx < seen[-1]:
                    v.append(f"L{i}: '### {name}' out of order under [{cur}]")
                seen.append(idx)
        elif FOOT_RE.match(l):
            footer_versions.append(FOOT_RE.match(l).group(1))
    for miss in sorted(set(header_versions) - set(footer_versions)):
        v.append(f"[{miss}] version section has no footer link")
    for miss in sorted(set(footer_versions) - set(header_versions)):
        v.append(f"[{miss}] footer link has no version section")
    return v

def lint_tags(csproj):
    with open(csproj, encoding="utf-8") as f:
        m = TAGS_RE.search(f.read())
    if not m:
        return []
    tags = [t.strip() for t in m.group(1).split(";") if t.strip()]
    lead = ["tunit", "assertions", "testing"] if csproj.endswith(".TUnit.csproj") else ["assertions", "testing"]
    v = []
    if tags[:len(lead)] != lead:
        v.append(f"{csproj}: PackageTags must start with '{';'.join(lead)}' (got '{';'.join(tags[:len(lead)])}')")
    if tags[-2:] != ["dotnet", "aot"]:
        v.append(f"{csproj}: PackageTags must end with 'dotnet;aot' (got '{';'.join(tags[-2:])}')")
    return v

if __name__ == "__main__":
    root = sys.argv[1] if len(sys.argv) > 1 else "."
    issues = []
    cl = os.path.join(root, "CHANGELOG.md")
    if os.path.isfile(cl):
        issues += [f"CHANGELOG.md: {x}" for x in lint_changelog(cl)]
    for cs in sorted(glob.glob(os.path.join(root, "src", "**", "*.csproj"), recursive=True)):
        issues += lint_tags(cs)
    if issues:
        print(f"FAIL ({len(issues)} issue(s)):")
        for x in issues:
            print("  - " + x)
        sys.exit(1)
    print("OK  changelog + package tags conform")
