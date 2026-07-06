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
