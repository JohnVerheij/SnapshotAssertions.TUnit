#!/usr/bin/env python3
"""Family lint gate (CI-safe: no sensitive terms). Run from a repo root with no
args; lints CHANGELOG.md (Keep a Changelog + family conventions) and every
src/**/*.csproj <PackageTags> (two-variant order). Exit 1 on any violation."""
import re, sys, glob, os

ALLOWED_H3 = {"Added", "Changed", "Deprecated", "Removed", "Fixed", "Security", "BREAKING"}
ORDER = ["Added", "Changed", "Deprecated", "Removed", "Fixed", "Security"]
VER_RE = re.compile(r"^## \[(\d+\.\d+\.\d+)\] - \d{4}-\d{2}-\d{2}: \S.*$")
VER_LOOSE = re.compile(r"^## \[(\d+\.\d+\.\d+)\]")
FOOT_RE = re.compile(r"^\[(\d+\.\d+\.\d+)\]:\s+(\S.*?)\s*$")
UNREL_FOOT_RE = re.compile(r"^\[[Uu]nreleased\]:\s+(\S.*?)\s*$")
TAGS_RE = re.compile(r"<PackageTags>(.*?)</PackageTags>")

def _ver(s):
    return tuple(int(p) for p in s.split("."))

def _order_issues(header_versions):
    out = []
    for a, b in zip(header_versions, header_versions[1:]):
        if _ver(a) <= _ver(b):
            out.append(f"versions must be newest-first: [{a}] should sit above [{b}], not below")
    return out

def _footer_issues(line, i, footer_versions):
    fm = FOOT_RE.match(line)
    if fm:
        footer_versions.append(fm.group(1))
        if not fm.group(2).rstrip().endswith(f"v{fm.group(1)}"):
            return [f"L{i}: footer [{fm.group(1)}] should target 'v{fm.group(1)}': {fm.group(2).strip()!r}"]
        return []
    um = UNREL_FOOT_RE.match(line)
    if um and not um.group(1).rstrip().endswith("HEAD"):
        return [f"L{i}: footer [unreleased] should target 'HEAD': {um.group(1).strip()!r}"]
    return []

def lint_changelog(path):
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
    v, header_versions, footer_versions = [], [], []
    if not any(line.strip() == "## [Unreleased]" for line in lines):
        v.append("missing '## [Unreleased]' section")
    if not any(line.strip().lower().startswith("[unreleased]:") for line in lines):
        v.append("missing '[unreleased]:' footer link")
    cur, seen, first_section = None, [], None
    for i, line in enumerate(lines, 1):
        if line.startswith("## ["):
            seen = []  # every version section starts a fresh order scope
            if first_section is None:
                first_section = line.strip()
                if first_section != "## [Unreleased]":
                    v.append(f"L{i}: first section must be '## [Unreleased]', found {first_section!r}")
            if line.strip() == "## [Unreleased]":
                cur = "Unreleased"
            else:
                m = VER_LOOSE.match(line)
                if m:
                    header_versions.append(m.group(1))
                    if not VER_RE.match(line):
                        v.append(f"L{i}: version header not '## [x.y.z] - YYYY-MM-DD: summary': {line.strip()!r}")
                    cur = m.group(1)
        elif line.startswith("#### "):
            v.append(f"L{i}: sub-header deeper than '###' not allowed: {line.strip()!r}")
        elif line.startswith("### "):
            name = line[4:].strip()
            if name not in ALLOWED_H3:
                v.append(f"L{i}: non-standard section '### {name}'")
            elif name in ORDER:
                idx = ORDER.index(name)
                if seen and idx < seen[-1]:
                    v.append(f"L{i}: '### {name}' out of order under [{cur}]")
                seen.append(idx)
        else:
            v += _footer_issues(line, i, footer_versions)
    v += _order_issues(header_versions)
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
    is_adapter = csproj.endswith(".TUnit.csproj")
    lead = ["tunit", "assertions", "testing"] if is_adapter else ["assertions", "testing"]
    v = []
    if tags[:len(lead)] != lead:
        v.append(f"{csproj}: PackageTags must start with '{';'.join(lead)}' (got '{';'.join(tags[:len(lead)])}')")
    if tags[-2:] != ["dotnet", "aot"]:
        v.append(f"{csproj}: PackageTags must end with 'dotnet;aot' (got '{';'.join(tags[-2:])}')")
    if not is_adapter and "tunit" in tags:
        v.append(f"{csproj}: core PackageTags must not contain 'tunit'")
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
