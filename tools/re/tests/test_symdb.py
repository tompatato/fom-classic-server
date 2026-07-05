"""Tests for the static symbol/type database.

The 2006 client's real disassembly data doesn't exist yet (recon Step 0), so
these tests validate the harness *machinery* against a small synthetic fixture
built in a tempdir — no Ghidra, no binary, no running game, no committed data.
They stay green from day one and start exercising real data automatically once
``disassembly/`` is populated (the CLI reads the same loader).

Run:  python3 -m unittest discover -s tools/re/tests
"""

import json
import struct
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import symdb  # noqa: E402


def _write(path: Path, doc) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(doc))


def build_fixture(root: Path) -> None:
    """Lay out a minimal, synthetic ``disassembly/``-shaped tree."""
    syms = root / "symbols"
    # Program A: sample.exe @ image base 0x00400000
    _write(syms / "sample.exe" / "_meta.json", {"imageBase": "0x00400000"})
    _write(syms / "sample.exe" / "App" / "Widget.json", {
        "functions": [
            {"addr": "0x00401230", "name": "DoThing", "namespace": "App::Widget"},
        ],
        "data": [
            {"addr": "0x00499000", "name": "g_counter", "namespace": ""},
        ],
    })
    # Bulk typed globals as a top-level list (like the exporter's _data shards).
    _write(syms / "sample.exe" / "_data" / "Sample.json",
           [{"addr": "0x0049a0%02x" % i, "name": "Sample_%d" % i, "namespace": ""}
            for i in range(8)])
    # Program B: engine.dll @ image base 0x10000000
    _write(syms / "engine.dll" / "_meta.json", {"imageBase": "0x10000000"})
    _write(syms / "engine.dll" / "_global.json", {
        "functions": [
            {"addr": "0x10005000", "name": "Tick", "namespace": "Engine"},
        ],
    })
    # Types: a struct and an enum.
    types = root / "types"
    _write(types / "Sample" / "Vec.json", {
        "path": "/Sample/Vec", "kind": "struct", "len": 12, "packed": False,
        "fields": [
            {"name": "x", "offset": 0, "len": 4, "type": "/float"},
            {"name": "y", "offset": 4, "len": 4, "type": "/float"},
            {"name": "next", "offset": 8, "len": 4, "type": {"ptr": "/Sample/Vec"}},
        ],
    })
    _write(types / "Sample" / "Status.json", {
        "path": "/Sample/Status", "kind": "enum", "len": 4,
        "entries": [{"name": "OK", "value": 0}, {"name": "FAIL", "value": 1}],
    })


class SymbolDbTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._tmp = tempfile.TemporaryDirectory()
        root = Path(cls._tmp.name)
        build_fixture(root)
        cls.db = symdb.SymbolDb(root).load()

    @classmethod
    def tearDownClass(cls):
        cls._tmp.cleanup()

    def test_programs_and_image_bases(self):
        progs = self.db.programs
        self.assertEqual(progs["sample.exe"], 0x00400000)
        self.assertEqual(progs["engine.dll"], 0x10000000)

    def test_symbols_loaded(self):
        self.assertGreater(len(self.db.all_symbols()), 5)

    def test_resolve_function_and_rva(self):
        matches = self.db.resolve("DoThing")
        self.assertTrue(matches)
        fn = matches[0]
        self.assertEqual(fn.program, "sample.exe")
        self.assertEqual(fn.addr, 0x00401230)
        self.assertEqual(fn.rva, 0x1230)
        self.assertEqual(fn.kind, "function")

    def test_resolve_qualified_name(self):
        matches = self.db.resolve("App::Widget::DoThing")
        self.assertTrue(any(m.addr == 0x00401230 for m in matches))

    def test_resolve_global_data(self):
        matches = self.db.resolve("Sample_3")
        self.assertTrue(matches)
        self.assertEqual(matches[0].kind, "data")
        self.assertEqual(matches[0].addr, 0x0049A003)

    def test_search_substring(self):
        hits = self.db.search("Sample_", kind="data")
        self.assertEqual(len(hits), 8)

    def test_type_layout_struct(self):
        vec = self.db.get_type("/Sample/Vec")
        self.assertIsNotNone(vec)
        self.assertEqual(vec["len"], 12)
        fields = {f["name"]: f for f in symdb.iter_fields(vec)}
        self.assertEqual(fields["x"]["offset"], 0)
        self.assertEqual(fields["next"]["offset"], 8)
        self.assertIn("ptr", fields["next"]["type"])

    def test_enum_value_name(self):
        self.assertEqual(self.db.enum_value_name("/Sample/Status", 1), "FAIL")
        self.assertIsNone(self.db.enum_value_name("/Sample/Status", 99))


class DecodeScalarTests(unittest.TestCase):
    def test_unsigned_and_signed(self):
        self.assertEqual(symdb.decode_scalar("/stdint.h/uint32_t",
                                             struct.pack("<I", 0xDEADBEEF)), 0xDEADBEEF)
        self.assertEqual(symdb.decode_scalar("/int", struct.pack("<i", -5)), -5)
        self.assertEqual(symdb.decode_scalar("/ushort", struct.pack("<H", 513)), 513)

    def test_bool_and_float(self):
        self.assertIs(symdb.decode_scalar("/bool", b"\x01"), True)
        self.assertAlmostEqual(symdb.decode_scalar("/float",
                                                   struct.pack("<f", 1.5)), 1.5)

    def test_pointer_dict(self):
        out = symdb.decode_scalar({"ptr": "/void"}, struct.pack("<I", 0x10203040))
        self.assertEqual(out, "0x10203040")

    def test_array_returns_raw(self):
        raw = b"\x00\x01\x02\x03"
        self.assertEqual(symdb.decode_scalar({"arr": "/uint8_t", "n": 4}, raw), raw)

    def test_short_buffer_returns_raw(self):
        # not enough bytes -> raw passthrough, never raises
        self.assertEqual(symdb.decode_scalar("/uint32_t", b"\x01\x02"), b"\x01\x02")


if __name__ == "__main__":
    unittest.main()
