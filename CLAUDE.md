# Face of Mankind (2006) — Clean-Room Emulator

This repository is a **from-scratch** reverse-engineering effort targeting the
**original 2006 release** of *Face of Mankind* (Duplex Systems), which runs on an
older engine than the 2011–2014 "Fall of the Dominion" (FotD) relaunch.

It is a **sibling, not a fork**, of the FotD emulator (a separate checkout of the
`fotd-server` repo). We inherit that project's *methodology and
tooling*; we do **not** assume its protocol, packet IDs, network library version,
or code transfer. Treat every wire-format fact as unknown until re-derived against
the 2006 client. See [`docs/relationship-to-fotd.md`](docs/relationship-to-fotd.md).

## Current phase

**Phase 0 — Recon.** No server exists yet. The immediate goal is to identify the
client binary, its network library and version, and the login/handshake wire
format. The full plan is in [`docs/recon-plan.md`](docs/recon-plan.md) — start
there. Do not scaffold servers or design architecture until Phase 0's exit
criteria are met.

## Working agreements

- **Clean room.** This project's conclusions must be justified from the 2006
  client itself, not carried over as assumptions from FotD. When you *do* lean on
  FotD prior art (very useful as a hypothesis source), say so explicitly and then
  confirm it against the 2006 binary before recording it as fact.
- **Never commit game binaries, assets, or master data.** They are copyrighted and
  live outside the tree (see `.gitignore`). Reference them by absolute path.
- **Document every RE deep dive** in the Obsidian client vault at
  `knowledge-base/client/` — Markdown with `[[wikilinks]]`, offset/RVA tables, and
  a `## Reproduce` section. Mirror the `_Template.md`. Mark unverified claims
  explicitly. Keep notes current rather than letting them drift.

## Toolchain

The Ghidra/JDK/Python RE toolchain lives **outside the repo**; the harness resolves
it via environment variables or a gitignored local config. Version constraints and
setup are in [`docs/toolchain.md`](docs/toolchain.md). Read it before running Ghidra
or reading live process memory — modern host defaults (a too-new JDK or Python,
SELinux enforcing) are all too new / too locked-down and
will fail in non-obvious ways otherwise.

## Code style (house conventions, carried over)

These apply once code exists; they are the same conventions as the FotD project so
the two read alike.

- **Acronym casing**: treat acronyms as words — capitalize only the first letter,
  regardless of length — then apply the language's normal casing. Examples:
  `PlayerId`, `WorldId`, `PacketIds`, `Db`, `Ip`, `HtmlParser`.
  - Exempt: `FOM` (proper noun, stays uppercase — `FOMNetwork`); in-game
    item/weapon/skill/apartment proper nouns (keep original master-data casing);
    `SCREAMING_SNAKE_CASE` wire constants and enum values (`ID_LOGIN`); external
    framework type names (`IPAddress`).
- **No empty property patterns**: never `x is { }`, `x is not { }`, or `x is { } y`.
  Use `is null` / `is not null`, and `.HasValue` + `.Value` when you need the
  value. Property patterns with real subpatterns (`x is { Status: Success }`) are
  fine.

## Git workflow

- Branch naming: `{type}/[{gh-issue}-]{short-summary}` where `{type}` ∈ `feat`,
  `fix`, `refactor`, `chore`, `docs`.
- Commit subject in Title Case, ≤50 chars; blank line; body wrapped at 72,
  focused on the **why**.
- No remote is configured yet — this is a local repo. Set one up when ready; do
  not push game binaries.
