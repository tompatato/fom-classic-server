"""Tests for the Ghidra-bridge plumbing that need neither Ghidra nor a project.

The actual decompile/xref need a built ``FoMClassic`` project + a Ghidra install,
so they are exercised manually; here we cover the pure logic: sentinel extraction
and target disambiguation (symbol name vs prog:addr). Symbol resolution runs
against the same synthetic fixture as ``test_symdb`` — no committed game data.
"""

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import fomre  # noqa: E402
import ghidra  # noqa: E402
import symdb  # noqa: E402
from test_symdb import build_fixture  # noqa: E402


class ExtractResultTests(unittest.TestCase):
    def test_extracts_from_noise(self):
        blob = ('INFO  Headless startup complete (HeadlessAnalyzer)\n'
                '@@FOMRE@@{"name": "Foo", "entry": "401230"}@@END@@\n'
                'INFO  REPORT: Import succeeded\n')
        out = ghidra.extract_result(blob)
        self.assertEqual(out["name"], "Foo")
        self.assertEqual(out["entry"], "401230")

    def test_no_sentinel_returns_none(self):
        self.assertIsNone(ghidra.extract_result("INFO just logs, no result\n"))


class ResolveTargetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._tmp = tempfile.TemporaryDirectory()
        build_fixture(Path(cls._tmp.name))
        cls.db = symdb.SymbolDb(cls._tmp.name).load()

    @classmethod
    def tearDownClass(cls):
        cls._tmp.cleanup()

    def test_prog_addr_form(self):
        prog, addr = fomre._resolve_target(self.db, "sample.exe:0x00401230")
        self.assertEqual(prog, "sample.exe")
        self.assertEqual(addr, 0x00401230)

    def test_cpp_qualified_name_not_mistaken_for_prog_addr(self):
        # contains ':' but must resolve as a symbol, not prog:addr
        prog, addr = fomre._resolve_target(self.db, "App::Widget::DoThing")
        self.assertEqual(prog, "sample.exe")
        self.assertEqual(addr, 0x00401230)


if __name__ == "__main__":
    unittest.main()
