# Ghidra Annotation Export — Face of Mankind (2006)

This directory preserves the hand-built Ghidra analysis of the 2006 client binaries
as **diffable, regenerable JSON**, so the labeling lives in version control
**without redistributing the game binary** (which we don't own). You bring your own
copy of a binary; one command rebuilds the fully-labeled Ghidra project on top of a
fresh import.

> **Empty until recon.** The 2006 client's modules haven't been catalogued yet
> (see [`docs/recon-plan.md`](../docs/recon-plan.md) Step 0), so `symbols/` and
> `types/` hold no data — only the scripts and this README. The layout below is
> what the exporter will produce.

## What's here

```
types/                       one JSON file per data type, organized by bucket then category
  <SharedLib>/…                types shared across modules
  stdint.h/  crtdefs.h/        external typedefs the binaries reference
  <module>/…                   a binary's OWN types
symbols/<program>/           the labeling, sharded by namespace
  namespaces.json              the class/namespace tree
  <Ns>/<Class>.json            a class: its functions + namespaced data + comments
  _global.json                 functions/data not in a user namespace
  _data/<Type>.json            bulk typed globals
scripts/
  export_program.py            dump one labeled program -> types/ + symbols/   (PyGhidra)
  build_program.py             reconstruct one program from JSON onto a fresh import
  validate_build.py            re-dump a built program in memory and diff vs the shards
  decompile.py / xref.py       on-demand decompile / xref (driven by the fomre harness)
```

The `just ghidra-gen` / `just ghidra-dump` recipes (in the repo's
[`justfile`](../justfile)) drive these scripts headless.

## What these files are — and aren't

The type JSON describes the *shapes* of the game's data structures (field names,
offsets, sizes, enum values, function prototypes) using structural references
(`{"ptr": ...}`, `{"arr": ..., "n": N}`). The symbol JSON is names, namespace
placement, signatures, and comments.

There is **no machine code, no disassembly, no decompiler output, no binary bytes,
and no addresses copied out of the binary**. This is reverse-engineering work
product — descriptions of structure, like documentation. The binary, the Ghidra
project (`*.gpr`/`*.rep`), and any `.gzf` are **never** committed (see
[`.gitignore`](../.gitignore)).

## Prerequisites

- **Ghidra 12.0.4** — pinned. The build layers annotations on top of a fresh
  auto-analysis, so the analyzer version must match; the scripts refuse to run
  otherwise. See [`docs/toolchain.md`](../docs/toolchain.md).
- **PyGhidra** (Python 3.10–3.13) — the scripts use it; the `just ghidra-*` recipes
  launch headless through it.
- **Your own copy** of the 2006 game binaries.

## Rebuild the labeled project (one command)

```
just ghidra-gen
```

Ghidra is located via `$GHIDRA_INSTALL_DIR`; your binaries via `$FOMC_GAME_DIR`
(default `./client`); the modules to import via `$FOMC_CLIENT_MODULES`
(comma-separated — set once recon Step 0 identifies the client's PE modules). The
project is built in place at `disassembly/FoMClassic`.

For each module it imports the binary, auto-analyzes, reconstructs the shared types
into a project archive inside the project and links the program to it, reconstructs
the binary's own types, recreates namespaces, applies every function name +
signature, names/types the globals, and restores comments — into one project.

## Re-export after more analysis

```
just ghidra-dump             # run with the project CLOSED in the GUI (headless needs the lock)
```

Workflow: label and create types in Ghidra; link the shared ones to the shared
archive; run `just ghidra-dump`; commit the JSON diff.

## Upgrading Ghidra later

Open the project on the new Ghidra, let it upgrade, re-run `just ghidra-dump` on
that version, and bump `EXPECTED_GHIDRA_VERSION` in the scripts.
