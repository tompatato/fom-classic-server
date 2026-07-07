# World Object Spawn (Object.lto ServerShell)

**The real 3D-avatar spawn lives in `Object.lto`, not `CShell.dll`.** The 2006
client runs the LithTech **game-object DLL (`Object.lto`) as an in-process local
server** (it registers `IServerShell` / `ILTServer` and defines the `CCharacter`
object class). The engine replicates that local server's objects to the local
client for rendering. Every visible character — yours and other players' — is a
`CCharacter` created here. `CShell`'s `0x082D` roster and the `ced8`/`ced4`
subsystems are UI/economy **data** only; injecting them never renders an avatar
(see [[Spawn Experiment]]). This supersedes the earlier CShell-only search.

> Status: ✅ statically derived from `Object.lto` (Ghidra, imported alongside
> CShell into the `FoMClassic` project). The exact transport that delivers the
> entity snapshot to the spawn method is not yet confirmed live — that's the next
> experiment. Complements [[TCP Message Dispatch]] (the CShell half).

## Architecture

- `Object.lto` = LithTech **server** object DLL, run locally. Interfaces resolved
  via LithTech's holder registry (`FUN_1002d1e0`): `s_IServerShell.Default` →
  singleton/vtable at **`0x10087030`** (held at `0x100b1e2c`); `s_ILTServer.Default`
  (engine server interface) → **`DAT_100b4280`**; `ModelLT`-style interface →
  **`DAT_100b2648`**. `CShell.dll` is the separate client shell.
- Engine interfaces are called by vtable offset off `DAT_100b4280` (server) — e.g.
  `[0x5c]` = find object class, `[0x60]` = CreateObject, `[0x108]` = SetObjectName,
  `[0x1c]` = console print, `[0x94]`/`[0x98]` = get/set rotation.

## ServerShell vtable (`0x10087030`, 20 methods)

Reconstructed by dumping the table (thunks tail-call the real handlers, each gated
on `DAT_100b42f0` = the live session/game object):

| idx | thunk | target | role |
| --- | --- | --- | --- |
| 18 | `1002ffb0` | **`FUN_100371d0`** | **`OnMessage(msg)`** — TCP message router (below) |
| 19 | `1002ffd0` | **`FUN_10036fc0`** | **entity-snapshot → spawn** (below) |

Lower indices are other IServerShell virtuals (several point at a shared empty stub
`FUN_1002da20`). Method 19 is engine-invoked (its only code xref is the vtable
slot), so the snapshot is delivered by the engine/transport, not the opcode router.

## OnMessage router `FUN_100371d0` (ServerShell TCP dispatch)

Logs the same *"TCP message received, ID %u, %u bytes"* string as CShell's router,
reads a u16 opcode, and dispatches. **This is the Object.lto half of the opcode
map** — disjoint from CShell's `FUN_10079840` (which owns `0x7de`–`0x83d`).

- `opcode == 0x7D2` → `FUN_1003ca80` (login-return)
- `0x3EA`–`0x3FF` → jump table `0x100373f0`:

| op | handler | note |
| --- | --- | --- |
| `0x3EA` | `FUN_10034ca0` | |
| `0x3EB` | `FUN_1003df00` | **ENTER_WORLD / level-login + precache** |
| `0x3EE` | `FUN_10034370` | |
| `0x3EF` | `FUN_10033820` | |
| `0x3F1` | `FUN_10036bf0` | |
| `0x3F5` | `FUN_10033760` | **appearance update** for an existing entity |
| `0x3F6` | `FUN_100402d0` | |
| `0x3F7` | `FUN_10033ab0` | |
| `0x3F8` | `FUN_10033e80` | |
| `0x3F9` | `FUN_10033940` | |
| `0x3FB` | (common tail) | |
| `0x3FC` | `FUN_100355e0` | |
| `0x3FD` | `FUN_10034990` | |
| `0x3FE` | `FUN_10034740` | |
| `0x3FF` | `FUN_100351d0` | |
| `0x3EC/ED/F0/F2/F3/F4/FA` | default | **`0x3F3` MOVEMENT is UDP, not here** |

- `0x7D7`–`0x831` → byte-index table `0x10037470` + jump table `0x10037448`; almost
  all default. Real handlers: `0x7D7`→`FUN_10034080`, `0x7D8`→`FUN_10036510`,
  `0x7D9`→`FUN_100341c0`, `0x7DD`→`FUN_1003aa10`, `0x7E6`→`FUN_100380e0`,
  `0x7EC`→`FUN_10033ce0`, `0x824`→`FUN_10033db0`, `0x82C`→`FUN_10032b20`,
  `0x831`→`FUN_100326f0`. (`0x82D` → default here — confirms it's CShell-only.)

## The spawn: `FUN_10036fc0` → `FUN_10035930`

`FUN_10036fc0(this=session `DAT_100b42f0`, snapshot)` walks an **entity snapshot**
and instantiates any character not already live (looked up by id in the object
manager `DAT_100b42e0` via `FUN_10004ee0`). The snapshot buffer struct is confirmed
identical across `FUN_10036fc0` (spawn), `FUN_10035b60` (post-pass, character), and
`FUN_10036350` (post-pass, objects):

- `snapshot+0x14` = **u16 count**; `snapshot+0x18` = array of **≤ 50 × 32-byte
  entries** (stride `0x20`; entries past index 49 clamp to `DAT_100b46f8/470c`).
  `FUN_10036fc0` does **not** deserialize (no `FUN_10041890` call) — it reads the
  struct directly, so an OnMessage handler or the engine hands it a ready buffer.
  Per entry it computes `entryType = (entry[0] >> 24) & 0xF`:
  - `entryType < 0xB` → **character**: if not already present and `!(entry[3] &
    0x10000000)`, call **`FUN_10035930(this, entry, &entry[7])`** to spawn.
  - else → non-character object: spawn via `FUN_100356c0` (CreateObject of class
    `DAT_10059f58`, flags `0x203001`, same entry position fields), or (appearance in
    `0x28B`–`0x2BA`) just update position `FUN_1001a510(obj, entry[3..5] as floats)`.

**`FUN_10035930` = create a `CCharacter`:**
```
class = DAT_100b4280[0x5c]("CCharacter")   // find object class
obj   = DAT_100b4280[0x60](class, ocs)     // CreateObject(class, ObjectCreateStruct)
obj[0x14] = entry[0]                        // store entity id  (obj+0x50)
FUN_10008080(obj, appearance)              // SetAppearance (below)
DAT_100b4280[0x108](obj[2], "<Unknown>")   // SetObjectName; obj[2] = HOBJECT
```
`ocs`: type = `8`, flags = `0x8203001`; position from the entry (see below).

### 32-byte entry layout

| off | field | meaning |
| --- | --- | --- |
| `0x00` | `id` | low 24 bits = entity id; bits 24–27 = type nibble |
| `0x04` | `pos.x \| pos.z` | low u16 = X, high u16 = Z (world units, signed) |
| `0x08` | `pos.y` | low u16 = Y |
| `0x0C` | `flags` | `&0x1FF` = node index (adds height), `&0x10000000` = skip-spawn, `&0xF000000` = variant |
| `0x10`,`0x14` | — | used as float x/y/z when *updating* a non-char object |
| `0x1C` | `appearance` | packed appearance code → `SetAppearance` |

Spawn position: base vector Y from node index `(entry[3]&0x1ff)*k - k2`, then
X=`(s16)entry[1]`, Z=`(s16)(entry[1]>>16)`, Y=`(s16)entry[2]`.

## Appearance code (packed u32) — `FUN_10008080` → `FUN_1002da30`

Same bitfield as CShell's `FUN_10014d10`. Builds `Models\Characters\{m_,f_,f2_}%u.ltb`
+ skin/head `.dtx`:

| bits | field |
| --- | --- |
| `0`–`4` (`0x1F`) | body/model index |
| `5`–`10` (`0x3F`) | face/head |
| `11`–`14` | texture set A |
| `15`–`18` | texture set B |
| `19`–`22` | texture set C |
| `23` (`0x800000`) | sex (set = female path `f_`/`f2_`) |
| `24`–`27` (`≤10`) | race/faction skin folder (`Neutral\`, …) |
| `28`–`30` | variant |

## ENTER_WORLD `FUN_1003df00` (0x3EB) — precache, not spawn

Deserializes (`FUN_10041890`), switches on a subtype. Subtype **4** = enter world:
reads a colony id (`0x28` = "Global Dominion Cloning Facility", else index into the
`New York City - Ground Zero`… colony-name table) and a count, then **precaches up
to 300 appearance codes** (`auStack_2a9c[300]`): character codes
(`(code & 0xF000000) < 0xA000001`) → `FUN_1002da30`, else object codes → precache.
Logs *"Level login caching done, took %.1f seconds"*. So ENTER_WORLD warms the
model cache for who's about to appear; the entities themselves arrive separately
and are spawned by `FUN_10036fc0`.

## Engine-side transport investigation (Lithtech.exe + server.dll)

Both binaries are now imported into the `FoMClassic` project. Findings on how the
shell methods get invoked:

- **Shell vtable identity confirmed.** The engine holds the shell interface at
  **`Lithtech.exe!DAT_0058aee4`**; it calls `*shell + 0x20` = method 8 =
  `OnClientEnterWorld` (the "…returned LTNULL!" error site, `FUN_00461b30`),
  matching Object.lto table[8]. So table `0x10087030` **is** the IServerShell
  vtable and `FUN_10036fc0` **is** method 19 (offset `0x4c`).
- The engine's `DAT_0058aee4` only calls methods **4, 5, 8, 9** (offsets
  `0x10/0x14/0x20/0x24`). Reliable messages reach the shell via **server.dll's
  `ServerInterface` vtable** (`0x10057cb0`…): `FUN_1002b301` forwards
  `OnObjectMessage` (shell m17, a no-op stub) and **`FUN_1002b31a` forwards
  `OnMessage` (shell m18 = `FUN_100371d0`, the TCP opcode router)**. There is **no
  ServerInterface forwarder for m19**, and no `+0x4c` shell call in server.dll.
- ⇒ **Method 19 is invoked directly by the engine on a threaded/indexed dispatch
  path** (not the reliable `OnMessage` route). Object.lto's IServerShell m17 (the
  standard object-message hook) is stubbed out, so FoM repurposed **m19 as its own
  state channel**. Given m18 = reliable TCP and m19 is the raw (non-deserialized)
  entity snapshot, m19 is almost certainly the **unreliable / UDP world-state
  channel** — consistent with movement `0x03F3` being UDP and ignored by m18.
- The engine UDP receive is **`Lithtech.exe!FUN_0047dab0`** (`recvfrom` via
  `Ordinal_17`, logs "UDP: recvfrom…"); it ref-counts the datagram into a queue
  (`FUN_0042b720`) consumed asynchronously — which is why the datagram→m19 path
  doesn't decompile as a straight call chain.

## What to try next

1. **Decisive: dynamic analysis (tooling ready — `tools/re/attach_spawn.sh`).**
   Break on `Object.lto!FUN_10036fc0` (and `FUN_10035930`) in the live client, then
   read `param_1` (the snapshot buffer: count@+0x14, 32-byte entries@+0x18) and the
   caller (`[esp]` — the thunk *jumps* in, so the return address is the real engine
   caller) to identify the socket/channel. See **Dynamic capture** below. That pins
   the exact transport + wire layout in one shot, versus chasing the threaded
   dispatch statically.
2. Cross-check the C# server: the **local** player already spawns as a `CCharacter`
   via this machinery, so our server already emits whatever seeds the entity table.
   Confirm whether the local avatar is even rendered (third-person body) — if so,
   m19 already fires with our current messages and only a second entry is missing.
3. Once the channel is known, emit a snapshot entry from `GameHost` (TCP or the
   existing UDP `SendToAsync`) with id + packed position + appearance and observe.

## Reproduce

`Object.lto`, `server.dll`, and `Lithtech.exe` are all imported into the
`FoMClassic` Ghidra project. Re-derive (use `-process <module>`):

```bash
# Object.lto:
#   ServerShell vtable:      dump_ptrs.py 0x10087030 20
#   OnMessage opcode table:  dump_ptrs.py 0x100373f0 22   (base opcode 0x3EA)
#   spawn:                   decompile.py 0x10035930   (CreateObject "CCharacter")
#   appearance:              decompile.py 0x10008080 ; 0x1002da30
#   snapshot walker:         decompile.py 0x10036fc0
# server.dll:
#   OnMessage forwarder:     decompile.py 0x1002b31a ; ServerInterface vtable 0x10057cb0
# Lithtech.exe:
#   engine shell ptr:        xref.py 0x0058aee4 to
#   UDP receive:             decompile.py 0x0047dab0
# helpers added this pass: find_vcalls.py <disp…>, find_indexed_calls.py
```

### Dynamic capture (live client)

Requires `ptrace_scope=0` and running as the same user as the client.

```bash
# 1. start the server, launch the client via Steam/Proton, LOG IN + ENTER THE WORLD
#    (Object.lto only loads once the local server/world is up)
# 2. attach + arm the spawn hook:
tools/re/attach_spawn.sh              # finds Lithtech.exe, breaks on the spawn fns
# gdb arms FUN_10036fc0 (snapshot walker) + FUN_10035930 (CCharacter create) and
# continues. Each hit logs: caller module (= transport), snapshot count + entries
# (id/type/x/z/y/flags/appearance). Ctrl-C then `quit` to detach.
```

What each outcome tells us:
- **Walker fires, `called from:` = engine UDP code** → transport is the UDP
  world-state channel; the buffer dump gives the wire layout to emit from `GameHost`.
- **Only `FUN_10035930` fires (for your own player)** → confirms the local avatar is
  a `CCharacter` via this path; read its caller to find the seeding message.
- **Neither fires in normal play** → the snapshot needs a trigger we don't yet send;
  m19 is dormant until a second entity is announced.

### Live capture result (2026-07-06)

Attached at world entry (gdb, signal-safe) with the stub server (login → leave
apartment). Observed:

- **`FUN_10008080` SetAppearance fired for the local player** — char obj
  `0xdad3c60`, appearance `0x71088820` (a real code: male, race nibble = 1). Its
  caller is **`FUN_10038600`**, *not* `FUN_10035930`.
- **`FUN_1003df00` ENTER_WORLD fired** (from OnMessage @ the `0x3EB` stub) — our
  TCP `0x3EB` reaches the handler.
- **`FUN_10035930` and the walker `FUN_10036fc0` never fired**, through world entry.

**Conclusion — two distinct spawn paths:**
- **Local player**: created via `ServerShell::OnClientEnterWorld` (m8) on the local
  LithTech client-connect; appearance applied by `FUN_10038600`. This is why you
  see *only* your own body.
- **Remote players**: `FUN_10036fc0` (walker) → `FUN_10035930`. **Remote-only and
  dormant** — never runs until the server delivers a world-state snapshot. The
  walker is engine-invoked from `Lithtech.exe` (server.dll has no `+0x4c` call);
  one of the 7 `IServerShell.Default`-lookup globals in the engine drives it.

So making a second avatar appear = deliver the entity snapshot that drives the
walker. Reusable known-good appearance code for a test entity: **`0x71088820`**.

### ✅ First server-driven spawn — CMeetingPoint via `0x3FE` (2026-07-06)

Implemented `SpawnMeetingPoint` (server `0x3FE`) and injected it after world entry.
**A `flag_station` object rendered in the world at the player's position** (named
"TESTFLAG"). Hook confirmed the handler `FUN_10034740` received the exact bytes:

```
03fe 002a 00001093 007f 003b 0dff 54455354464c4147 00…
op   len  id       x    y    z    "TESTFLAG"
```

Confirmed facts:
- **Wire body of an Object.lto entity message = fields from struct+0x10, in order,
  big-endian.** For `0x3FE`: `id (u32) · x (u16) · y (u16) · z (u16) · name (char[32])`.
  The client validates opcode + size (≤ capacity `0x2c`), then `CreateObject`s.
- **Entity position uses the same coordinate system as UDP movement `0x03F3`** —
  sending the player's last-known movement X/Y/Z placed the object exactly on them.
- This is the general recipe for the other per-type spawns (`0x3FD` CAdvertisement,
  etc.): one opcode per object class, `CreateObject` in the handler.

This proves server→client world-object spawning. **Characters are still the one
gap** — they have no per-type opcode (only the engine-invoked walker), so the
remaining work is triggering the walker with a character snapshot.

### 🔬 Character-snapshot experiment — apparatus (2026-07-07)

Built the server side to drive the walker `FUN_10036fc0`. `FOM_SNAPSHOT_TEST=1`
makes `GameHost` push a **single character world-state snapshot** to a player over
**UDP** (once, right after they start sending movement), placing a second character
beside them (`id = playerId + 4243`, position = player's last movement with X+0x100,
appearance `0x71088820`). Builder: `CharacterSnapshot` (Protocol layer).

**Candidate wire layout (datagram, absolute offsets):**

| off | field | endian | note |
| --- | --- | --- | --- |
| `0x00` | opcode | BE | candidate `0x03F3` — guess; only affects engine routing |
| `0x02` | body length | BE | `= len - 4`, mirrors movement framing |
| `0x04`–`0x13` | reserved | — | zero; engine header layout unconfirmed |
| `0x14` | `u16 count` | **LE** | walker reads count here |
| `0x16` | pad | — | zero |
| `0x18`+ | `32-byte entries` | **LE** | walker reads raw struct (no deserialize) |

Entry fields (LE): `0x00` = `(type<<24)|id`, `0x04` = `X|(Z<<16)`, `0x08` = `Y`,
`0x0C` = flags(0), `0x1C` = appearance. **Endianness = little-endian** because the
walker reads the buffer as a native x86 struct (it never calls the deserializer
`FUN_10041890`) — the one hard inference; everything else (header, routing opcode,
whether it's UDP at all) is a hypothesis this experiment tests.

**To run:** rebuild Release, start with `FOM_SNAPSHOT_TEST=1` (add
`FOM_SNAPSHOT_REPEAT=1` to resend every 500 ms so a live hook can't miss it), launch
the client, enter world, **move**, and arm the gdb hook (`tools/re/attach_spawn.sh <pid>`
— pass the **real** in-world pid; `pgrep` also matches Steam's `reaper` wrapper, so
pick the pid whose `/proc/<pid>/maps` contains `Object.lto`).

### ❌ LIVE RESULT (2026-07-07): app-UDP does NOT drive the walker

Ran it against the live client with the hook armed and a **positive control**
(`FOM_SPAWN_TEST=1` → the proven `0x3FE` CMeetingPoint). Hits over the session:

| breakpoint | fired | meaning |
| --- | --- | --- |
| `FUN_10034740` (0x3FE CMeetingPoint) | **3** | positive control — flag rendered; TCP OnMessage path + hook both live |
| `FUN_1003df00` (ENTER_WORLD 0x3EB) | 3 | our TCP `0x3EB` reaches its handler |
| `FUN_10008080` (SetAppearance) | 2 | local player, appearance `0x71088820` |
| **`FUN_10036fc0` (snapshot walker)** | **0** | — |
| `FUN_10035930` (CCharacter create) | 0 | — |

**111 snapshots were sent over UDP; the walker fired 0 times.** The datagram *is*
delivered to the client's socket — the client's movement socket is a **connected**
UDP socket (`ESTAB 127.0.0.1:<eph> → 127.0.0.1:<worldport>`), and our reply goes out
from the world-port socket, so its source matches the connected peer and the kernel
accepts it. So the LithTech **engine receives our datagram but does not route it to
ServerShell m19.** The candidate `0x03F3`-framed app datagram is the wrong thing.

**Conclusion — the character snapshot is not an app-opcode UDP datagram.** App
opcodes (`0x3EA`–`0x3FF`, `0x7xx`) ride the **reliable TCP** channel into OnMessage
(m18 = `FUN_100371d0`) — that's how `0x3FE` and `0x3EB` got in and rendered. The
snapshot buffer that m19 walks must be **populated by one of the other OnMessage
handlers** (TCP), after which the engine's m19 pass spawns the entries — *not* raw
UDP. (Movement `0x3F3` being UDP is a red herring: it's the client's outbound app
data, not the engine's inbound replication.)

### 🔎 STATIC DEEP DIVE (2026-07-07): the walker's buffer is engine-supplied

Chased "who calls the walker and with what buffer" through Ghidra. Findings:

- **`CCharacter` is created ONLY by the walker.** `FUN_10035930` (the sole
  `CreateObject "CCharacter"`) has exactly **one** caller — `FUN_10036fc0` at
  `10037174`. All other `"CCharacter"` string xrefs (`FUN_10037c70`, `FUN_10023680`,
  `FUN_100171a0`, `FUN_10023870`) are just **class-handle lookups** (`[0x5c]`
  find-class / `[0x170]` get-class), not create sites. So there is **no per-type
  TCP opcode** for characters — unlike world objects (`0x3FE` has its own handler
  that `CreateObject`s directly). A locally-created object *is* replicated to the
  renderer (that's why the `0x3FE` flag appears), so the absence of any other
  creator is conclusive: everything routes through the walker.
- **The walker's only caller is the vtable[19] thunk** (`xref FUN_10036fc0` → one
  ref, `1002ffda`). So `param_1` (the snapshot buffer) is supplied by whoever
  invokes m19 — the engine.
- **Not server.dll.** `server.dll` has **zero** `[reg+0x4c]` vtable calls, and the
  m18 forwarder `FUN_1002b31a` is only a data ref from the ServerInterface vtable
  slot `0x10057cf8`. So m19 is invoked by **Lithtech.exe directly**, not via the
  ServerInterface.
- **Static offset search can't pin the engine caller.** `0x4c` (= method 19) is a
  ubiquitous vtable slot — 150+ `[reg+0x4c]` sites in Object.lto, dozens in the
  engine. The one clean engine candidate that passes a stack *buffer* to `+0x4c`
  (`FUN_0045eb40` via `DAT_0058ab94`, from the client frame update `FUN_0040fac0`)
  is **object position interpolation** (its `+0x4c` returns a float vector;
  `DAT_0058ab94`/`DAT_0058ab8c` are physics/model interfaces, not the shell — its
  `+0x44` call is used as a live method, but shell m17 is a stub, so it's not the
  shell). `find_indexed_calls` on the engine surfaces only switch jumptables and a
  generic hashtable template (`FUN_00424780`), not the shell dispatch.

**Conclusion:** the character snapshot is delivered by the **LithTech engine's own
object-replication / threaded dispatch path**, not by the FoM app-opcode layer we
drive for everything else (login, world, chat, movement, world-objects). Pinning the
exact engine trigger is blocked statically by vtable-offset collision.

### ➡️ Next: decouple the two unknowns dynamically

The remaining unknowns are **(a) the entry/buffer format** and **(b) the transport
that makes the engine call m19**. Solve (a) independently of (b) with gdb by
**manually invoking the walker** on a hand-built buffer. Tooling: `tools/re/invoke_walker.py`
(+ `invoke_walker.sh` wrapper). It mallocs a buffer in the inferior, writes
`count=1 @+0x14` and one 32-byte entry `@+0x18` (id/type, packed pos, flags,
appearance — native LE, as the walker reads raw), sets `ECX = DAT_100b42f0`
(thiscall `this`), and calls `FUN_10036fc0(buf)`; a logging breakpoint on
`FUN_10035930` reports whether the CCharacter creator was reached.

```bash
# client in-world, then:
tools/re/invoke_walker.sh <x> <y> <z>     # coords from the server's latest MOVE log
```

If an avatar renders (and the creator breakpoint fires), the entry format is
**confirmed** and only transport (b) remains; if not, fix the fields the walker reads
and retry. This turns the hard transport question into a checkable one. For (b) the
likely path is reversing the engine UDP recv `Lithtech.exe!FUN_0047dab0` → its async
queue consumer → the m19 dispatch, or capturing real 2006-server traffic if any
exists. (The earlier "find the OnMessage handler that writes the buffer" idea is
**ruled out** — no Object.lto handler writes it; the engine does.)

### ❌ NEGATIVE RESULT (2026-07-07): gdb-invoking the walker crashes the client

Ran `invoke_walker.sh` against the live client (in-world). Notes for anyone
repeating this:

- **The setup works; the call does not.** After stripping the confirmation
  breakpoint (a breakpoint hit *inside* a gdb inferior call hangs batch gdb, which
  froze the client ~2 min and killed it) and the extra object-lookup call (hung /
  faulted under the SIGSEGV-pass needed for Wine), the minimal call succeeded from
  gdb's view: `malloc` in-inferior returned a buffer, `ECX = session` (thiscall
  `this`), `FUN_10036fc0(buf)` **returned 0 and gdb detached cleanly**.
- **But the client froze on resume and had to be force-killed** (coredump =
  `pthread_exit`/`abort_thread` forced unwind — the signature of a manual kill of a
  wedged process). So calling the walker **outside the engine's server-update tick**
  returns but leaves the game inconsistent: `FUN_10035930`'s `CreateObject` →
  appearance/model resolution → object registration isn't safe to run out-of-context.
- Cost each attempt: the client must be relaunched + re-enter world. Three crashes.

**Conclusion: the entry format cannot be validated by out-of-context invocation in
this environment.** `tools/re/invoke_walker.{py,sh}` are kept as a documented
dead-end with a warning header — do not run against a client you care about.

### ➡️ Real next step: reverse the transport (or capture real traffic)

Format and transport can't be decoupled cheaply, so go after **transport directly**:
1. **Reverse the engine's inbound world-state path.** `Lithtech.exe!FUN_0047dab0`
   (UDP `recvfrom`) → the datagram queue `FUN_0042b720` → the async consumer that
   eventually calls shell m19. Decompile the queue consumer / netmsg dispatcher to
   learn the LithTech engine packet format that carries object state, then emit that
   from `GameHost` — this is engine (netmgr) framing, a level below the FoM app
   opcodes we already speak.
2. **Or capture real 2006-server traffic** (any pcap/retail server) to read the
   actual character-spawn bytes on the wire — the cleanest ground truth if a source
   exists.

### 🔬 TRANSPORT RE (2026-07-07): the engine inbound path is bit-packed netmgr

Decompiled the engine UDP receive `Lithtech.exe!FUN_0047dab0`:

- `Ordinal_17` (`recvfrom`) into an 8168-byte stack buffer; on success it wraps the
  datagram as a **ref-counted bit-stream message** — `FUN_00456410(&msg, buf,
  bytes * 8)` (note `* 8`: the length is tracked in **bits**, i.e. LithTech
  `ILTMessage`, which is **bit-packed**, not byte-aligned) — then pushes it through a
  smart-pointer slot `FUN_0042b720` (release-old / assign / incref) → `FUN_00409810`
  (enqueue/notify). Consumed asynchronously by a reader thread.
- `FUN_0047dab0` is called from the engine socket loops `FUN_00481020`,
  `FUN_004832d0`, `FUN_00483940` — stock LithTech netmgr.

**So the engine's inbound world-state channel is LithTech's bit-packed netmgr
message layer** — a different, lower level than FoM's plain `[op][len][body]` app
protocol that our server speaks (and that the client uses for movement, which we
parse as plain bytes). To drive m19 through here we'd have to reproduce LithTech's
netmgr framing + the bit-packed object-update message — a big lift.

**⚠️ Pivotal open question before investing there: does FoM even USE netmgr, or
bypass it?** Everything our plain server drives (login, world, chat, movement,
`0x3FE`) is plain and byte-aligned, which suggests FoM may bypass netmgr for app
traffic — in which case `FUN_0047dab0` is a red herring and m19's buffer comes from
FoM's own code. **Answer this cheaply and SAFELY first** with a *logging-only* gdb
breakpoint (the spawn-hook style never destabilized the client — only the
inferior-call invoke did) on `FUN_0047dab0` (engine base + RVA `0x7dab0`) plus the
walker `FUN_10036fc0`, while the plain server runs: if the engine recv fires on our
traffic, netmgr is in play and worth reversing; if it never fires, FoM bypasses it
and the m19 trigger is elsewhere (re-open the FoM-side search, incl. CShell.dll and
the client's own UDP handler).

### ✅ ANSWERED (2026-07-07): FoM BYPASSES netmgr — `FUN_0047dab0` is a red herring

Ran the logging hook (`spawn_hook.py`, now with the engine-recv breakpoint at
`Lithtech.exe base 0x400000 + 0x7dab0 = 0x47dab0`, inserted clean) against the live
client while the server streamed **100 UDP snapshots** to it (`FOM_SNAPSHOT_TEST=1
FOM_SNAPSHOT_REPEAT=1`). Session hit counts:

| breakpoint | fired |
| --- | --- |
| `FUN_10008080` SetAppearance | 2  ← positive control (hook is live this session) |
| `FUN_1003df00` ENTER_WORLD | 3  ← positive control |
| **`FUN_0047dab0` engine UDP recv** | **0** |
| `FUN_10036fc0` walker | 0 |

The hook provably fires (SetAppearance/ENTER_WORLD) yet the engine netmgr UDP recv
caught **none** of our 100 datagrams — which *are* delivered to the client socket
(connected UDP, source = world port). **⇒ FoM services its UDP with its own code;
LithTech netmgr is not in the inbound path.** So reversing `FUN_0047dab0` → m19 is
**ruled out** — abandon the netmgr-framing plan.

### ➡️ Next: find FoM's own inbound handler that seeds the m19 snapshot

With netmgr out, m19 is almost certainly driven by the engine's **per-tick server
update** building the snapshot from the local Object.lto server's object/entity
state — and that state is seeded by **FoM's own code** on inbound app traffic. Hunt:
1. **FoM's own UDP recv** (not the engine's): find the code that reads the client's
   movement socket (`recvfrom`/`Ordinal_17` xrefs in **CShell.dll** / **Object.lto**,
   not Lithtech.exe). A message type there may add a remote entity.
2. **The remaining OnMessage (m18, TCP) handlers** — only 3 of ~10 were decompiled
   (`0x3EA`,`0x3F1`,`0x3FC`); check the rest (`0x3EE`,`0x3EF`,`0x3F6`–`0x3F9`,`0x3FF`)
   for a write into the object manager `DAT_100b42e0` / a pending-spawn list the
   per-tick m19 pass then consumes. `0x3F5` is "appearance update for an existing
   entity", so the character-*add* sibling is the target.
3. Reconsider what actually invokes m19 per tick (the engine server-update path), now
   that the netmgr hypothesis is dead.
