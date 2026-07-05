# Relationship to the FotD Emulator

The **FotD emulator** (the `fotd-server` repo — check it out as a sibling
alongside this one) targets the 2011–2014 "Fall of the Dominion" relaunch
(RakNet 3.611, client build 1.8.5.3). This project targets the **2006 original**.
They are separate projects with a shared game lineage. FotD is **prior art and a
hypothesis source** — not a dependency.

## What transfers (expensive stuff already paid for)

- **Methodology** — the observe → RE → verify loop (`docs/re-methodology.md`).
  Engine-agnostic.
- **Tooling knowledge** — the Ghidra/JDK/Python toolchain and host gotchas
  (`docs/toolchain.md`).
- **The `fomre` RE harness** — largely binary-agnostic; strong port candidate
  (see below).
- **Domain knowledge** — factions, contracts, worlds, items, the master/world
  server split. Same game design lineage; the *concepts* hold even where the
  *encoding* differs.
- **Architectural patterns** — managed Core/Application/Infrastructure split,
  native-abstracts-RakNet layering, persistence approach. Good bones to copy.

## What resets (must be re-derived against the 2006 client)

- **The wire protocol** — packet IDs, BitStream layouts, serializers. Assume all
  of it differs until proven.
- **The network library version** — FotD is RakNet 3.611. The 2006 client's
  library/version is **unknown** and is the pivotal Phase-0 question
  (`docs/recon-plan.md` Step 1). It decides fork-vs-rewrite for the whole net layer.
- **Client module layout** — FotD splits into `fom_client.exe` + `CShell.dll` +
  `Object.lto`. The 2006 split is unknown.
- **Runtime** — FotD runs via a Steam-Proton shortcut; the 2006 client predates
  that and likely needs a plain Wine prefix.

## Reference map — where the FotD prior art lives

All paths are relative to the root of the `fotd-server` checkout:

| Prior art | Path | Use as |
| --- | --- | --- |
| RakNet 3.611 vendored source | `extern/raknet` | Version-compare / signature-match against the 2006 net lib. **Never modify.** |
| RE harness (`fomre`) | `tools/re/` | Port candidate — see below. |
| Harness usage guide | `tools/re/README.md` | How the static DB + live memory + Ghidra bridge fit together. |
| Static symbol/type DB (Ghidra export) | `disassembly/` | Schema reference for our own export; FotD-specific data. |
| Client RE deep dives (Obsidian vault) | `knowledge-base/client/` | Hypothesis source. `Login Handshake.md`, `Packet Transport.md`, `Item Definitions.md`, etc. |
| Architecture write-up | `docs/architecture.md` | Threading/routing/persistence patterns to consider adopting. |
| Adding-packet-handlers guide | `docs/adding-packet-handlers.md` | The end-to-end pattern, once we have servers. |

There are also repo-scoped Claude **skills** in the FotD project (`fom-network`,
`item-table`, `string-table`, `managing-packet-structures`, `raknet`). They encode
**FotD-specific** (RakNet 3.611) knowledge — useful background, but do not treat
their contents as true for the 2006 protocol.

## Porting the `fomre` harness

The harness is a good early port because its core is binary-agnostic:

- `symdb.py` — indexes the exported symbol/type JSON. Generic; only the *data*
  (module names, addresses) is FotD-specific.
- `memory.py` — finds the PID, recovers module load bases, reads/scans
  `/proc/<pid>/mem`. Fully generic.
- `ghidra.py` — headless-Ghidra decompile/xref bridge. Generic given a built
  project and `ghidra.local.json`.
- `fomre.py` — the CLI tying them together.

To port: copy `tools/re/` into this repo, then rebuild the `disassembly/` export
from the **2006** client (new module names, new symbols) rather than reusing
FotD's. The `ghidra.local.json` paths (`docs/toolchain.md`) carry over unchanged.
Recommended once recon Steps 0–1 give us module names and a first Ghidra project.
