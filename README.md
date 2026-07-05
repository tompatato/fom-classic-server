# Face of Mankind (2006) — Clean-Room Server Emulator

A from-scratch reverse-engineering effort targeting the **original 2006 release**
of *Face of Mankind* (Duplex Systems), which runs on an **older engine** than the
2011–2014 "Fall of the Dominion" (FotD) relaunch.

This is a **greenfield** project. There is no server code yet — the current phase
is **reconnaissance**: identifying the client, its network library, and its wire
protocol before any architecture is committed.

## Status

🟡 **Phase 0 — Recon.** Nothing is built. Nothing is assumed.

## Where to start

👉 **[`docs/recon-plan.md`](docs/recon-plan.md)** — the phased kickoff plan. Read
this first.

Then:

- [`docs/re-methodology.md`](docs/re-methodology.md) — the proven observe → RE →
  implement → verify loop, adapted for a cold start.
- [`docs/toolchain.md`](docs/toolchain.md) — where the Ghidra/JDK/Python RE
  toolchain lives and the gotchas that will otherwise eat an afternoon.
- [`docs/relationship-to-fotd.md`](docs/relationship-to-fotd.md) — what carries
  over from the sibling FotD emulator, what resets, and where the prior art lives.
- [`knowledge-base/client/`](knowledge-base/client/) — the Obsidian vault where RE
  deep dives get written up.

## Relationship to the FotD emulator

This project is **completely separate** from the FotD emulator (a sibling checkout;
its exact location is recorded in [`docs/relationship-to-fotd.md`](docs/relationship-to-fotd.md)).
That project targets the *relaunch* (RakNet 3.611, client build 1.8.5.3). We reuse
its **methodology and tooling knowledge**, not its code or its protocol — the 2006
engine's wire format is a different animal and is treated as unknown until proven
otherwise.

## Legal / ethical

Clean-room RE of a discontinued product for interoperability and preservation. **Do
not commit copyrighted game binaries, assets, or master data to this repo** — see
`.gitignore`. Binaries live outside the tree and are referenced by path.
