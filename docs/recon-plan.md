# Recon Plan — Phase 0

**Goal of this phase:** answer one question with evidence, not assumption —
*how much of a server emulator do we actually have to build from scratch, and what
can be adapted from the FotD prior art?* The single biggest lever on that answer is
**which network library the 2006 client uses, and which version.** Everything else
is downstream of that.

Do not design servers or a project structure yet. Recon first.

---

## Step 0 — Acquire and stage the client

**Exit criteria:** the 2006 client is on disk at a known path, launchable, and its
main modules identified.

- [ ] Obtain the original 2006 *Face of Mankind* client (Duplex Systems). This is a
      standalone installer era — **not** the Steam/Proton shortcut the FotD client
      uses, so the runtime story is different (see Step 3).
- [ ] Stage it **outside this repo**, at a path of your choosing, and reference it
      by absolute path from your notes. Client material is gitignored; never commit
      it.
- [ ] Inventory the modules: main `.exe`, and any engine/UI DLLs (the FotD relaunch
      splits into `fom_client.exe` + `CShell.dll` + `Object.lto`; the 2006 build's
      split is **unknown** — record what it actually is).
- [ ] Note file versions, timestamps, PE compile stamps, and any embedded version
      strings.

> ⚠️ Record the module layout you find as fact. Do **not** assume it mirrors FotD.

---

## Step 1 — Identify the network library and version ⭐ (highest-value step)

**Exit criteria:** we can name the network library and pin its version, and we know
whether FotD's `FOMNetwork` (RakNet 3.611) is a *fork candidate* or *reference
only*.

This determines whether the wire layer is a fork or a rewrite. RakNet has existed
since ~2004; the 2006 client could use **RakNet 2.x, an early RakNet 3.x, or
something else entirely**. The BitStream format, `MessageIdentifiers` enum base,
compression, and connection handshake all shift across RakNet major/minor versions.

Techniques (cheapest first):

- [ ] **String scan** the binaries for `RakNet`, version strings, `RakPeer`,
      `BitStream`, `ID_CONNECTION_REQUEST`, and copyright banners. RakNet embeds
      recognizable strings.
- [ ] **Import table** inspection — is networking in a separate DLL or static?
- [ ] **Ghidra signature match**: pull the RakNet 3.611 sources (vendored in the
      sibling `fotd-server` checkout at `extern/raknet`) and compare
      function shapes / constants (e.g. the `MessageIdentifiers` enum ordering,
      offline-message IDs) against the 2006 binary.
- [ ] If RakNet: pin the **exact version** and locate matching upstream source for
      that version. If not RakNet: identify what it is before proceeding.

Record findings in `knowledge-base/client/` (e.g. a `Network Library.md` note).

---

## Step 2 — Capture and decode the login handshake

**Exit criteria:** a documented byte-level login/connect flow for the 2006 client,
with a first `knowledge-base/client/Login Handshake.md` note.

- [ ] Identify how the client reaches its master/login server (command-line arg,
      config file, hardcoded host?). The FotD client uses `+MasterServer <ip>`;
      the 2006 client's mechanism is unknown.
- [ ] Point it at a local endpoint. A **passive listener / packet sink** on the
      expected UDP port is enough to capture the first outbound packets — you do
      not need a working server to observe the connect + login request.
- [ ] Cross-reference captured bytes against the RakNet version from Step 1
      (offline connection handshake) and the Ghidra decompilation of the client's
      login-packet builder.
- [ ] Document the login packet(s): field offsets, types, the version/build number
      the client sends, and any integrity/fingerprint fields.

> FotD prior art (`knowledge-base/client/Login Handshake.md` in the sibling repo)
> is an excellent **hypothesis source** — same game lineage, likely similar
> concepts (two-round request→credentials, MD5-based hash, CRC integrity triplet).
> Use it to know *what to look for*, then confirm every field against the 2006
> binary before recording it.

---

## Step 3 — Establish the client runtime loop

**Exit criteria:** the 2006 client runs reliably under Wine/Proton, and we can read
its live process memory.

- [ ] Get it launching (Wine prefix or Steam-Proton shim). The FotD keyboard/OEM
      gotcha (XWayland `us` vs UK `gb` layout) may recur — see `docs/toolchain.md`.
- [ ] Confirm live memory access works: `kernel.yama.ptrace_scope = 0` and
      same-UID. This unlocks the `read`/`scan`/`struct` live-inspection half of the
      RE harness.

---

## Step 4 — Decision gate

**Exit criteria:** a short written decision (add it to this file or a new
`docs/architecture-decision.md`) covering:

- [ ] Network layer: **fork** FotD's `FOMNetwork` (if same/near RakNet version) or
      **rewrite**. Justify from Step 1 evidence.
- [ ] How much of FotD's managed structure (Core/Application/Infrastructure,
      master/world split, persistence) to adopt vs. rebuild.
- [ ] Whether to **port the `fomre` RE harness** from FotD now (it is largely
      binary-agnostic — see `docs/relationship-to-fotd.md`) — recommended once
      Steps 0–1 give us module names and a symbol export.

Only after this gate do we scaffold servers and adopt a project structure.

---

## Working notes

- Keep this checklist live — tick items and jot findings inline as you go.
- Every non-trivial RE result becomes a vault note under
  `knowledge-base/client/` (methodology in `docs/re-methodology.md`).
- When a step contradicts a FotD assumption, that contradiction is itself a
  finding worth recording — it is the whole point of the clean-room approach.
