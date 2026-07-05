# `fomre` — Reverse-engineering harness

Tooling that makes the Face of Mankind (2006) client's internals queryable from
the command line: it reads the committed static RE database under
[`disassembly/`](../../disassembly) and bridges it to the **live client process**
running under Wine/Proton.

This harness was ported from the FotD emulator; it is **binary-agnostic
machinery**. None of FotD's reverse-engineered data came with it — `disassembly/`
is empty until recon catalogues the 2006 client (see
[`docs/recon-plan.md`](../../docs/recon-plan.md)). The static commands therefore
return nothing until then; the code, tests, and live-memory half all work now.

## Layout

```
tools/re/
  symdb.py     static symbol + type database over disassembly/*.json (stdlib only)
  memory.py    live process memory: find PID, module bases, read/write/scan via /proc
  ghidra.py    headless-Ghidra bridge: decompile / xref against the FoMClassic project
  fomre.py     CLI tying them together
  tests/       unittest suite over a synthetic fixture (no game/Ghidra, CI-safe)
disassembly/scripts/
  decompile.py one function -> decompiled C (PyGhidra postScript, JSON out)
  xref.py      references to/from an address or function (PyGhidra postScript)
  build_program.py / export_program.py / validate_build.py  project (re)build + dump
```

## The three questions this answers

**1. What makes the binary accessible from the CLI?**
The hand-built Ghidra analysis is exported to diffable JSON (functions with
addresses/signatures/namespaces, typed globals, struct/enum layouts with field
offsets). `symdb.py` indexes it: resolve a name to an address, list a class's
members, print a struct's layout, map an enum value to its name.

**2. Does Ghidra have a CLI?**
Yes — `analyzeHeadless` and **PyGhidra** (`pyghidra_launcher.py --headless`).
`just ghidra-gen` / `just ghidra-dump` (see
[`disassembly/README.md`](../../disassembly/README.md)) rebuild/export the labelled
project headless. `ghidra.py` adds on-demand **decompile** and **xref** on top.

**3. Application memory values?**
The Windows client runs as a normal Linux process under Wine/Proton, so its PE
modules are file-backed mappings in `/proc/<pid>/maps`. `memory.py` finds the
process, recovers each module's **load base** (modules get relocated), and
reads/scans `/proc/<pid>/mem`. A static symbol's live address is
`module_base + (addr - imageBase)`.

## Usage

```bash
# --- static (needs disassembly/ populated; empty until recon) ---
just re programs                       # modules + image bases
just re sym <name>                     # search symbols
just re sym <name> --exact             # name -> addr / RVA
just re type <TypeName>                # struct field layout / enum members

# --- live (client running under Wine/Proton) ---
just re pid                            # find client + module bases
just re read <module>:0xADDR --type ptr
just re struct <module>:0xADDR /Type/Path
just re scan u32 1000                  # find addresses holding 1000

# --- Ghidra (needs the FoMClassic project; see disassembly/README.md) ---
just re decompile <SymbolOrModule:0xADDR>
just re xref <SymbolOrModule:0xADDR> --direction from
```

(`just re …` runs `tools/re/fomre.py …`; call the script directly if you prefer.)

## Which modules is "the client"?

`memory.py` needs the client's PE module filenames to find the process — and the
2006 client's module set is **unknown until recon Step 0**. Resolution order:

1. modules `fomre` already knows from the symbol DB (`disassembly/symbols/*`);
2. the `FOMC_CLIENT_MODULES` env var (comma-separated, e.g.
   `FOMC_CLIENT_MODULES=client.exe,engine.dll`);
3. otherwise a clear "modules not catalogued yet" error — never a guess.

The same `FOMC_CLIENT_MODULES` list drives which binaries `just ghidra-gen`
imports, so declare it once.

## Ghidra setup (for `decompile` / `xref` / `ghidra-*`)

Needs **Ghidra 12.0.4** (the version the `disassembly/` export is pinned to), a
**JDK 21–24**, and **PyGhidra on Python 3.10–3.13**. Resolution:

- Ghidra: `$GHIDRA_INSTALL_DIR` → `tools/re/ghidra.local.json` `install_dir` → `/opt/ghidra`
- JDK: `$FOMC_GHIDRA_JDK` / `$JAVA_HOME` → `ghidra.local.json` `jdk`

`tools/re/ghidra.local.json` (gitignored) holds this machine's paths:

```json
{ "install_dir": "/path/to/ghidra_12.0.4_PUBLIC", "jdk": "/path/to/jdk-21" }
```

Without any of this, `decompile`/`xref` raise a clear `GhidraUnavailable` — the
static DB and live-memory commands are unaffected. Full version rationale is in
[`docs/toolchain.md`](../../docs/toolchain.md).

A read/struct/decompile target is either a **symbol name** (resolved via the DB) or
an explicit `program:0xADDR` (the in-image address, as stored in the JSON) — use
the latter when a name exists in more than one module.

## Live-memory requirements

Reading another process's memory needs ptrace access: **same UID** as the client
**and** `kernel.yama.ptrace_scope = 0` (or run the harness as the client's parent).
On `EACCES`/`EPERM` the tool prints the reason and the
`sudo sysctl kernel.yama.ptrace_scope=0` remedy. Writes are **off by default**; set
`FOMC_RE_ALLOW_WRITE=1` to enable `write_mem`.

## Tests

```bash
just re-test          # or:
python3 -m unittest discover -s tools/re/tests
```

The suite validates symbol resolution, RVA math, image bases, struct/enum layouts,
and scalar decoding against a **synthetic in-memory fixture** — it needs neither
Ghidra, a game binary, nor any committed data, so it runs in CI and is green from
day one. Once `disassembly/` is populated, the same loader serves real data.
