# RE Methodology

The reverse-engineering loop proven on the FotD emulator, adapted for a cold start.
This is engine-agnostic — it worked against the relaunch client and applies
directly to the 2006 client. Only the binaries and the resulting facts differ.

## The core loop

```
observe → reverse-engineer → hypothesize → implement → verify → document
```

1. **Observe** client behavior — what does it send, when, and what does it expect
   back? Passive packet capture, log files, and live memory inspection.
2. **Reverse-engineer** the relevant code in Ghidra — find the packet builder /
   handler, read the struct layout, trace the field sources.
3. **Hypothesize** the wire format and semantics. FotD prior art is a strong
   hypothesis source (same game lineage) — but a hypothesis, not a fact.
4. **Implement** the minimal server-side piece that exercises it (later phases).
5. **Verify** end-to-end against the live client — the client is the oracle. A
   handler that "looks right" but the client rejects is wrong.
6. **Document** in the client vault (`knowledge-base/client/`) with offsets, RVAs,
   and a `## Reproduce` section.

The discipline that made this work on FotD: **the running client is the source of
truth.** Decompilation tells you what *should* happen; live capture and live memory
tell you what *does*. When they disagree, trust the live behavior and re-read the
code.

## Three ways to interrogate the binary

Ported from the FotD `fomre` harness (see `docs/relationship-to-fotd.md` for how to
lift it). Each is independently useful:

1. **Static symbol/type database** — a hand-labelled Ghidra analysis exported to
   diffable JSON (functions with addresses/RVAs/signatures, typed globals,
   struct/enum layouts with field offsets). Queryable from the CLI with no game and
   no Ghidra running. This is the durable, committable artifact.
2. **Live process memory** — the Windows client runs under Wine/Proton as a normal
   Linux process; its PE modules are file-backed mappings in `/proc/<pid>/maps`.
   Recover each module's relocated load base, then read/scan `/proc/<pid>/mem`. A
   static symbol's live address is `module_base + (addr − imageBase)`.
3. **Headless Ghidra** — `analyzeHeadless` / PyGhidra postScripts for on-demand
   **decompile** and **xref** against the labelled project.

## Clean-room discipline

- Justify conclusions from the 2006 client, not from FotD.
- When you use FotD as a hypothesis source, say so, then confirm against the 2006
  binary before recording as fact.
- Mark unverified claims explicitly in vault notes (e.g. *(role unverified)*).
- A contradiction with a FotD assumption is a finding — record it.

## Documentation convention

Every deep dive → a Markdown note in `knowledge-base/client/`:

- `[[wikilinks]]` to related notes.
- Offset/RVA tables for structs and packets.
- A `## Reproduce` section listing the exact commands (Ghidra decompile targets,
  type lookups, memory reads) that regenerate the finding.
- Explicit markers on anything not yet verified against the live client.

See `knowledge-base/client/_Template.md`.
