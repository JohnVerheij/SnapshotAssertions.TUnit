#!/usr/bin/env python3
"""Tests for the docs internal-link checker. Stdlib unittest only (no pytest
dependency); run with `python3 .github/test_docs_linkcheck.py`. Loads
docs-linkcheck.py by path because its filename is not a valid module
identifier."""
import importlib.util
import os
import tempfile
import unittest

_HERE = os.path.dirname(os.path.abspath(__file__))
_SPEC = importlib.util.spec_from_file_location(
    "docs_linkcheck", os.path.join(_HERE, "docs-linkcheck.py")
)
dl = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(dl)


class _Base(unittest.TestCase):
    def setUp(self):
        self._td = tempfile.TemporaryDirectory()
        self.docs = os.path.join(self._td.name, "docs")
        os.makedirs(self.docs)
        self.addCleanup(self._td.cleanup)

    def page(self, name, text=""):
        with open(os.path.join(self.docs, name), "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)

    def check(self):
        return dl.check_docs(self.docs)


class LinkCheckTests(_Base):
    def test_resolving_link_has_no_issues(self):
        self.page("changelog.md")
        self.page("index.md", "[c](changelog.md) and [with anchor](changelog.md#top)")
        self.assertEqual(self.check(), [])

    def test_dead_link_flagged(self):
        self.page("index.md", "[gone](missing.md)")
        self.assertTrue(any("dead internal link 'missing.md'" in x for x in self.check()))

    def test_link_escaping_docs_root_flagged(self):
        self.page("index.md", "[up](../secret.md)")
        self.assertTrue(any("escapes the docs root" in x for x in self.check()))

    def test_external_links_ignored(self):
        self.page("index.md", "[a](https://example/x.md) [b](mailto:x@example.md) [c](//host/x.md)")
        self.assertEqual(self.check(), [])

    def test_anchor_and_non_markdown_ignored(self):
        self.page("index.md", "[a](#section) [b](../src/Foo.cs) [c](image.png)")
        self.assertEqual(self.check(), [])

    def test_image_links_ignored(self):
        # '![alt](target)' is an image embed, not a page link.
        self.page("index.md", "![shield](missing.md)")
        self.assertEqual(self.check(), [])

    def test_reference_style_dead_link_flagged(self):
        self.page("index.md", "See [the changelog][cl].\n\n[cl]: missing.md")
        self.assertTrue(any("dead internal link 'missing.md'" in x for x in self.check()))

    def test_reference_style_resolving_and_external_ignored(self):
        self.page("changelog.md")
        self.page("index.md", "[a][x] [b][y]\n\n[x]: changelog.md\n[y]: https://example/z.md")
        self.assertEqual(self.check(), [])


if __name__ == "__main__":
    unittest.main(verbosity=2)
