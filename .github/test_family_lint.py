#!/usr/bin/env python3
"""Tests for the family lint gate. Stdlib unittest only (no pytest dependency);
run with `python3 .github/test_family_lint.py`. Loads family-lint.py by path
because its filename is not a valid module identifier."""
import importlib.util
import os
import tempfile
import unittest

_HERE = os.path.dirname(os.path.abspath(__file__))
_SPEC = importlib.util.spec_from_file_location(
    "family_lint", os.path.join(_HERE, "family-lint.py")
)
fl = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(fl)

# A minimal, fully-conforming changelog. Every invalid fixture below is a single
# mutation of this baseline, so a test failure points at exactly one rule.
VALID = """\
# Changelog

## [Unreleased]

## [1.1.0] - 2026-06-02: second release
### Added
- `Thing.New()` does a thing.
### Fixed
- `Thing.Old()` no longer misbehaves.

## [1.0.0] - 2026-06-01: first release
### Added
- `Thing.Old()` does a thing.

[unreleased]: https://example.com/compare/v1.1.0...HEAD
[1.1.0]: https://example.com/compare/v1.0.0...v1.1.0
[1.0.0]: https://example.com/releases/tag/v1.0.0
"""


def _matches(issues, needle):
    return any(needle in issue for issue in issues)


class _Base(unittest.TestCase):
    def setUp(self):
        self._td = tempfile.TemporaryDirectory()
        self.dir = self._td.name
        self.addCleanup(self._td.cleanup)

    def changelog(self, text):
        path = os.path.join(self.dir, "CHANGELOG.md")
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
        return fl.lint_changelog(path)

    def csproj(self, name, tags):
        path = os.path.join(self.dir, name)
        body = tags if tags is None else f"<Project><PropertyGroup><PackageTags>{tags}</PackageTags></PropertyGroup></Project>"
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(body or "<Project></Project>")
        return fl.lint_tags(path)


class ChangelogTests(_Base):
    def test_valid_changelog_has_no_issues(self):
        self.assertEqual(self.changelog(VALID), [])

    def test_missing_unreleased_section_flagged(self):
        text = VALID.replace("## [Unreleased]\n\n", "")
        self.assertTrue(_matches(self.changelog(text), "missing '## [Unreleased]' section"))

    def test_missing_unreleased_footer_flagged(self):
        text = VALID.replace("[unreleased]: https://example.com/compare/v1.1.0...HEAD\n", "")
        self.assertTrue(_matches(self.changelog(text), "missing '[unreleased]:' footer link"))

    def test_first_section_must_be_unreleased(self):
        text = VALID.replace("## [Unreleased]\n\n", "")
        self.assertTrue(_matches(self.changelog(text), "first section must be '## [Unreleased]'"))

    def test_sub_header_deeper_than_h3_flagged(self):
        text = VALID.replace("### Added\n- `Thing.New()`", "### Added\n#### Nested\n- `Thing.New()`", 1)
        self.assertTrue(_matches(self.changelog(text), "sub-header deeper than '###'"))

    def test_non_standard_section_flagged(self):
        text = VALID.replace("### Added\n- `Thing.New()`", "### Notes\n- `Thing.New()`", 1)
        self.assertTrue(_matches(self.changelog(text), "non-standard section '### Notes'"))

    def test_out_of_order_sections_flagged(self):
        # Fixed (index 4) before Added (index 0) under the same version.
        text = VALID.replace(
            "### Added\n- `Thing.New()` does a thing.\n### Fixed\n- `Thing.Old()` no longer misbehaves.",
            "### Fixed\n- `Thing.Old()` no longer misbehaves.\n### Added\n- `Thing.New()` does a thing.",
        )
        self.assertTrue(_matches(self.changelog(text), "out of order"))

    def test_version_header_without_summary_flagged(self):
        text = VALID.replace("## [1.0.0] - 2026-06-01: first release", "## [1.0.0] - 2026-06-01")
        self.assertTrue(_matches(self.changelog(text), "version header not"))

    def test_malformed_version_header_flagged(self):
        # '## [1.0]' is neither '## [Unreleased]' nor a valid 'x.y.z' header.
        text = VALID.replace("## [1.0.0] - 2026-06-01: first release", "## [1.0] - 2026-06-01: first release")
        self.assertTrue(_matches(self.changelog(text), "malformed version header"))

    def test_ascending_version_order_flagged(self):
        text = VALID.replace(
            "## [1.1.0] - 2026-06-02: second release", "## [0.9.0] - 2026-06-02: second release"
        ).replace("[1.1.0]: https://example.com/compare/v1.0.0...v1.1.0",
                  "[0.9.0]: https://example.com/compare/v1.0.0...v0.9.0")
        self.assertTrue(_matches(self.changelog(text), "newest-first"))

    def test_footer_wrong_target_flagged(self):
        text = VALID.replace(
            "[1.0.0]: https://example.com/releases/tag/v1.0.0",
            "[1.0.0]: https://example.com/releases/tag/v9.9.9",
        )
        self.assertTrue(_matches(self.changelog(text), "should target 'v1.0.0'"))

    def test_unreleased_footer_wrong_target_flagged(self):
        text = VALID.replace(
            "[unreleased]: https://example.com/compare/v1.1.0...HEAD",
            "[unreleased]: https://example.com/compare/v1.1.0...main",
        )
        self.assertTrue(_matches(self.changelog(text), "should target 'HEAD'"))

    def test_version_section_without_footer_flagged(self):
        text = VALID.replace("[1.0.0]: https://example.com/releases/tag/v1.0.0\n", "")
        self.assertTrue(_matches(self.changelog(text), "[1.0.0] version section has no footer link"))

    def test_footer_without_version_section_flagged(self):
        text = VALID.replace(
            "[1.0.0]: https://example.com/releases/tag/v1.0.0",
            "[1.0.0]: https://example.com/releases/tag/v1.0.0\n[0.5.0]: https://example.com/releases/tag/v0.5.0",
        )
        self.assertTrue(_matches(self.changelog(text), "[0.5.0] footer link has no version section"))


class TagTests(_Base):
    ADAPTER = "Foo.TUnit.csproj"
    CORE = "Foo.csproj"

    def test_valid_adapter_tags(self):
        self.assertEqual(self.csproj(self.ADAPTER, "tunit;assertions;testing;logging;dotnet;aot"), [])

    def test_valid_core_tags(self):
        self.assertEqual(self.csproj(self.CORE, "assertions;testing;logging;dotnet;aot"), [])

    def test_adapter_wrong_lead_flagged(self):
        issues = self.csproj(self.ADAPTER, "assertions;tunit;testing;dotnet;aot")
        self.assertTrue(_matches(issues, "must start with 'tunit;assertions;testing'"))

    def test_core_wrong_lead_flagged(self):
        issues = self.csproj(self.CORE, "testing;assertions;logging;dotnet;aot")
        self.assertTrue(_matches(issues, "must start with 'assertions;testing'"))

    def test_core_containing_tunit_flagged(self):
        issues = self.csproj(self.CORE, "assertions;testing;tunit;dotnet;aot")
        self.assertTrue(_matches(issues, "must not contain 'tunit'"))

    def test_wrong_trailer_flagged(self):
        issues = self.csproj(self.CORE, "assertions;testing;logging;aot;dotnet")
        self.assertTrue(_matches(issues, "must end with 'dotnet;aot'"))

    def test_csproj_without_tags_is_ignored(self):
        self.assertEqual(self.csproj(self.CORE, None), [])


if __name__ == "__main__":
    unittest.main(verbosity=2)
