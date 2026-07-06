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

- [x] Obtain the original 2006 *Face of Mankind* client (Duplex Systems). This is a
      standalone installer era — **not** the Steam/Proton shortcut the FotD client
      uses, so the runtime story is different (see Step 3).
      → Have `fom_openbeta_v1213` (repackaged 2024; core modules dated 2005).
- [x] Stage it **outside this repo**, at a path of your choosing, and reference it
      by absolute path from your notes. Client material is gitignored; never commit
      it.
      → Staged outside the repo as `<your-stage-dir>/fom_openbeta_v1213/`; point
      `FOMC_GAME_DIR` at it for the `just` recipes.
- [x] Inventory the modules: main `.exe`, and any engine/UI DLLs (the FotD relaunch
      splits into `fom_client.exe` + `CShell.dll` + `Object.lto`; the 2006 build's
      split is **unknown** — record what it actually is).
      → Engine is **LithTech** (`Lithtech.exe` + `dtype{lay,pwr,std}.dll`), launched
      via `FOM.exe`; game code in `Resources/CShell.dll` + `Object.lto` + `CRes.dll`;
      a `server.dll` is also present. MSVC 7.1 runtime (VS2003 toolchain). The
      `CShell.dll`/`Object.lto` split **mirrors** FotD — hypothesis source only.
- [ ] Note file versions, timestamps, PE compile stamps, and any embedded version
      strings. *(partial: filesystem timestamps + ImageBase noted; PE compile stamps
      and version resources not yet pulled.)*

> ⚠️ Record the module layout you find as fact. Do **not** assume it mirrors FotD.

---

## Step 1 — Identify the network library and version ⭐ (highest-value step)

**Exit criteria:** we can name the network library and pin its version, and we know
whether FotD's `FOMNetwork` (RakNet 3.611) is a *fork candidate* or *reference
only*.

> ✅ **RESOLVED (2026-07-06): it is NOT RakNet.** The client speaks a **custom
> protocol over raw Winsock** — a length-prefixed **TCP** main channel
> (`[opcode:u16 BE][len:u16 BE][body]`) plus a **UDP** movement channel, port
> `7500 + WorldId`. So FotD's RakNet `FOMNetwork` is **reference only → rewrite**
> (settles the Step 4 network decision too). Full write-up:
> `knowledge-base/client/Network Library.md`; framing/opcodes implemented and
> byte-tested in the `FOM.Protocol` C# library (`dotnet test FOM.slnx`).

Techniques (cheapest first):

- [x] **String scan** the binaries for `RakNet`, version strings, `RakPeer`,
      `BitStream`, `ID_CONNECTION_REQUEST`, and copyright banners.
      → **0 RakNet hits** in any module.
- [x] **Import table** inspection — is networking in a separate DLL or static?
      → imports **`WSOCK32.dll`** (Winsock 1.1); `Lithtech.exe` has
      `udp_BuildSockaddrFromString` (engine's own UDP helper).
- [ ] **Ghidra signature match** — no longer needed for library ID; reserve for
      decoding individual opcode bodies against the client.
- [x] If RakNet: pin the version … / If not RakNet: identify what it is.
      → Custom Winsock TCP+UDP; see the note.

Findings recorded in `knowledge-base/client/Network Library.md`.

---

## Step 2 — Capture and decode the login handshake

**Exit criteria:** a documented byte-level login/connect flow for the 2006 client,
with a first `knowledge-base/client/Login Handshake.md` note.

- [x] Identify how the client reaches its master/login server (command-line arg,
      config file, hardcoded host?). The FotD client uses `+MasterServer <ip>`;
      the 2006 client's mechanism is unknown.
      → **Hardcoded**: a table of ten IPv4 strings in `CShell.dll` `.rdata`
      (`82.133.85.42–52`), just before the `NETMGRCL` marker. See
      `knowledge-base/client/Network Address Table.md`. Master-vs-world role of the
      ten slots still open.
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

- [x] Get it launching (Wine prefix or Steam-Proton shim). The FotD keyboard/OEM
      gotcha (XWayland `us` vs UK `gb` layout) may recur — see `docs/toolchain.md`.
      → **Launches under Proton Experimental** (Steam non-Steam shortcut) driving
      `Lithtech.exe` directly; renders to the login screen. Raw Fedora Wine fails
      (no audio backend → sound-init null-deref). Full method:
      `docs/running-the-client.md`; RE detail: `knowledge-base/client/Launcher
      Bootstrap.md`. This also satisfies Step 0's "launchable" exit criterion.
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
