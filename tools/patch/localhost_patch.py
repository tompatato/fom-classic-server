#!/usr/bin/env python3
"""Redirect the 2006 Face of Mankind client to a server of your choosing.

The client hardcodes its server pool as a table of ten fixed-width, NUL-padded
IPv4 dotted-quad strings inside ``Resources/CShell.dll``, immediately preceding
the ``NETMGRCL`` (network-manager) marker. The stock build points at the
original Duplex Systems production block ``82.133.85.42-52``. This tool rewrites
every slot in that table to a target address (default ``127.0.0.1``) *in place*,
preserving the DLL's byte length so no PE headers, relocations, or checksums
shift.

This is a reproducible RE artifact, NOT a redistributed binary: it reads YOUR
own staged copy of the copyrighted client and edits it. It ships no game bytes.

Why search-and-replace rather than hardcoded file offsets: offsets are
build-specific and undocumented; matching the known host strings is robust,
self-documenting, and fails loudly if the input isn't the build we expect.

## Reproduce

    python3 tools/patch/localhost_patch.py --game-dir /path/to/fom_openbeta_v1213

Verify it reproduces a known-good hand patch:

    python3 tools/patch/localhost_patch.py --game-dir <dir> --backup
    md5sum <dir>/Resources/CShell.dll   # -> d1dc3fb656eebf9fd6481b3a38aab539
"""
from __future__ import annotations

import argparse
import hashlib
import shutil
import sys
from pathlib import Path

# The stock server pool baked into CShell.dll, in table order (a contiguous
# 82.133.85.42-52 block with .47 absent). Recorded as an RE fact; see
# knowledge-base/client/ "Network Address Table".
ORIGINAL_HOSTS = [
    "82.133.85.52", "82.133.85.51", "82.133.85.50", "82.133.85.49",
    "82.133.85.48", "82.133.85.46", "82.133.85.45", "82.133.85.44",
    "82.133.85.43", "82.133.85.42",
]

# md5 of the pristine v1213 CShell.dll, and of a known-good localhost patch.
ORIGINAL_MD5 = "e967cb411970ba7ad12599b00fe6a897"
KNOWN_PATCHED_MD5 = "d1dc3fb656eebf9fd6481b3a38aab539"

# Each address lives in a 16-byte slot ("255.255.255.255" + NUL = 16). A target
# longer than 15 chars cannot fit without overrunning into the next slot.
SLOT_MAX = 15


def _md5(data: bytes) -> str:
    return hashlib.md5(data).hexdigest()


def find_cshell(game_dir: Path) -> Path:
    hits = sorted(game_dir.rglob("CShell.dll"))
    if not hits:
        sys.exit(f"error: no CShell.dll found under {game_dir}")
    if len(hits) > 1:
        sys.exit("error: multiple CShell.dll found; point --game-dir at one client:\n  "
                 + "\n  ".join(str(h) for h in hits))
    return hits[0]


def patch(data: bytes, addr: str) -> tuple[bytes, int]:
    """Return (patched_bytes, slots_rewritten). Overwrites each stock host
    string with `addr`, NUL-filling the remainder of that string's byte run so
    the total length is unchanged."""
    if len(addr) > SLOT_MAX:
        sys.exit(f"error: target address {addr!r} exceeds the {SLOT_MAX}-char slot width")
    buf = bytearray(data)
    repl = addr.encode("ascii")
    rewritten = 0
    for host in ORIGINAL_HOSTS:
        needle = host.encode("ascii")
        start = buf.find(needle)
        if start == -1:
            continue
        # Only ever write within the original string's own byte run (repl is
        # shorter than every stock host), then NUL-fill the rest of that run.
        run = len(needle)
        buf[start:start + run] = repl + b"\x00" * (run - len(repl))
        rewritten += 1
    return bytes(buf), rewritten


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game-dir", type=Path, required=True,
                    help="staged client directory (contains Resources/CShell.dll)")
    ap.add_argument("--addr", default="127.0.0.1",
                    help="target server address (default: 127.0.0.1; max 15 chars)")
    ap.add_argument("--backup", action="store_true",
                    help="save CShell.dll.orig alongside before writing")
    ap.add_argument("--check", action="store_true",
                    help="report patch status and exit without writing")
    args = ap.parse_args()

    target = find_cshell(args.game_dir)
    data = target.read_bytes()
    digest = _md5(data)

    already = all(h.encode("ascii") not in data for h in ORIGINAL_HOSTS)
    if args.check:
        state = ("pristine" if digest == ORIGINAL_MD5
                 else "already patched (no stock hosts present)" if already
                 else "modified/unknown")
        print(f"{target}\n  md5:   {digest}\n  state: {state}")
        return

    if digest != ORIGINAL_MD5:
        note = "already patched" if already else "unexpected build"
        print(f"warning: CShell.dll md5 {digest} != known pristine "
              f"{ORIGINAL_MD5} ({note}); proceeding anyway", file=sys.stderr)

    patched, n = patch(data, args.addr)
    if n == 0:
        sys.exit("error: no stock host strings found — nothing to patch "
                 "(already redirected, or not the expected build)")
    if n != len(ORIGINAL_HOSTS):
        print(f"warning: rewrote {n}/{len(ORIGINAL_HOSTS)} slots "
              "(expected all); build may differ", file=sys.stderr)

    if args.backup:
        bak = target.with_suffix(target.suffix + ".orig")
        if not bak.exists():
            shutil.copy2(target, bak)
            print(f"backed up -> {bak}")

    target.write_bytes(patched)
    out_md5 = _md5(patched)
    print(f"patched {n} slot(s) -> {args.addr}\n  {target}\n  md5: {out_md5}")
    if args.addr == "127.0.0.1":
        match = "matches" if out_md5 == KNOWN_PATCHED_MD5 else "DIFFERS from"
        print(f"  {match} known-good localhost patch ({KNOWN_PATCHED_MD5})")


if __name__ == "__main__":
    main()
