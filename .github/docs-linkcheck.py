#!/usr/bin/env python3
"""Offline internal-link check for the generated docs sources (CI-safe: no
network). Run after the docs/*.md files are copied and their cross-file links
rewritten, before `dotnet docfx`. Fails if any inline Markdown link to a local
Markdown page (a target ending in '.md') does not resolve to an existing file
inside the docs root, or escapes it. External links (http/https/mailto/...),
bare '#anchor' links, and links to non-Markdown repo files are intentionally
not checked, so the gate stays deterministic and offline. Exit 1 on any dead
link."""
import glob
import os
import re
import sys

# Inline '[text](target)' links; the leading (?<!!) skips '![image](...)'.
_LINK = re.compile(r"(?<!!)\[[^\]]*\]\(([^)]+)\)")
_EXTERNAL = ("http://", "https://", "mailto:", "tel:", "ftp://", "//")


def _target(dest):
    """Extract the path portion of a link destination: drop <>, a trailing
    "title", and any #anchor / ?query."""
    dest = dest.strip()
    if dest.startswith("<") and ">" in dest:
        dest = dest[1:dest.index(">")]
    parts = dest.split()
    dest = parts[0] if parts else ""
    return dest.split("#", 1)[0].split("?", 1)[0]


def _escapes(root_abs, target):
    try:
        return os.path.commonpath([root_abs, target]) != root_abs
    except ValueError:  # different drive on Windows
        return True


def check_docs(root):
    root_abs = os.path.abspath(root)
    issues = []
    for path in sorted(glob.glob(os.path.join(root, "*.md"))):
        with open(path, encoding="utf-8") as f:
            text = f.read()
        base = os.path.dirname(os.path.abspath(path))
        name = os.path.basename(path)
        for m in _LINK.finditer(text):
            dest = m.group(1).strip()
            if not dest or dest.startswith("#") or dest.lower().startswith(_EXTERNAL):
                continue
            frag = _target(dest)
            if not frag.lower().endswith(".md"):
                continue
            target = os.path.normpath(os.path.join(base, frag))
            if _escapes(root_abs, target):
                issues.append(f"{name}: link '{frag}' escapes the docs root")
            elif not os.path.isfile(target):
                issues.append(f"{name}: dead internal link '{frag}'")
    return issues


if __name__ == "__main__":
    root = sys.argv[1] if len(sys.argv) > 1 else "docs"
    found = check_docs(root)
    if found:
        print(f"FAIL ({len(found)} dead link(s)):")
        for x in found:
            print("  - " + x)
        sys.exit(1)
    print(f"OK  internal doc links resolve ({root})")
