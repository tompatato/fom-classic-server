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

## What to try next

1. Find the transport that delivers the 32-byte-entry snapshot to method 19
   (`FUN_10036fc0`) — likely a specific message (possibly on UDP, like movement
   `0x03F3`, which OnMessage explicitly ignores) or an engine object-replication
   path. Grep `Object.lto` for a producer that writes count@+0x14 / 0x20-byte
   entries@+0x18.
2. Cross-check the C# server: the **local** player already spawns as a `CCharacter`
   via this machinery, so our server already emits whatever seeds the entity table.
   Find that path and add a second entry (id + packed pos + appearance) to make a
   second avatar appear. This is the live experiment.

## Reproduce

```bash
# Object.lto is imported into the FoMClassic Ghidra project. Re-derive:
#   ServerShell vtable:      dump_ptrs.py 0x10087030 20
#   OnMessage opcode table:  dump_ptrs.py 0x100373f0 22   (base opcode 0x3EA)
#   spawn:                   decompile.py 0x10035930   (CreateObject "CCharacter")
#   appearance:              decompile.py 0x10008080 ; 0x1002da30
#   snapshot walker:         decompile.py 0x10036fc0
```
