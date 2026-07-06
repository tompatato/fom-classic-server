# Spawn Experiment (0x082D)

Can we make **another player visible** in the client by sending a `0x082D` zone
update carrying one player entry? This is the gating unknown for multiplayer
visibility. The packet is implemented (`SpawnZoneUpdate`) and byte-pinned to the
reference stub, and the server can inject it after world entry — but whether the
client renders a **3D avatar** (vs. only a UI roster row) is **not yet confirmed**.
Related: [[Session Opcodes]], [[Network Library]].

> Status: 🟡 **Root cause found — empty roster (2026-07-06).** `0x082D` is the right
> handler, and the layout was essentially right too. The stub sent the array's
> **object count = 0 with a zeroed array**, so the client had zero entities to
> spawn. Fix applied in `SpawnZoneUpdate` (count = 1 + a populated `object[0]`);
> live re-test pending. See "Deserializer".

## Deserializer (entry format + root cause)

The `0x082D` data class (slots 0–2 of vtable `PTR_FUN_100fa8fc`) confirms the
struct, and the per-entry serializer `FUN_1009fd80` gives the entry format:

- **Object array** of **50 × 80-byte entries** at message-object `+0x64`
  (= payload `+0x54`), preceded by a **u16 count at `+0x60`** (payload `+0x50`),
  byteswapped via `Ordinal_9`/`_15` (`htons`). Trailing string at payload `+0xFF4`.
- **Each 80-byte entry** = 7× big-endian u32 (`Ordinal_8` = `htonl` on the first 6,
  then `FUN_100a7ec0`) followed by a **52-byte name** — matching the stub's
  "7×u32 + 52-byte name" note. Field 0 = id, field 1 = appearance.

These payload offsets (count `+0x50`, objects `+0x54`, string `+0xFF4`) **match the
stub's layout** once you account for the message object's `+0x10` base header. So
the layout was fine — the stub just set **count = 0** and left every entry zeroed,
giving the client an empty roster. `SpawnZoneUpdate` now writes `count = 1` and a
populated `object[0]` (id, appearance, name).

## Ghidra findings (handler located)

`CShell.dll` has a **dedicated message class for `0x082D`** (ctor `FUN_100a5970`,
vtable `PTR_FUN_100fa8fc`, methods at `100a5a00/5930/5a20/5be0/5cc0`). Its base
initializer `FUN_100a3c60(this, 0x82d, 0x17f4, 0x17f4, buf)` records:

| Field | Off | Value | Meaning |
| --- | --- | --- | --- |
| vtable | `+0` | `PTR_LAB_100fa808` | base message-class vtable |
| type | `+4` | `0x48` | message category |
| msgId | `+6` | `0x082D` | opcode |
| capacity | `+8` | `0x17F4` (6132) | **max** buffer size (not fixed) |
| cur size | `+10` | computed | serialized length, e.g. `count*0x50 + base` |
| buffer | `+0xc` | ptr | payload storage |

The serialization/size methods reveal the payload is **arrays of `0x50` (80)-byte
object entries** guarded by a **30-bit count** (`count & 0x3FFFFFFF`) plus a
trailing NUL-terminated string — but at **different offsets** than the reference
stub guessed (stub put count at `+0x50`, objects at `+0x54`; the real class keys
off a count field and 80-byte slots elsewhere). So the stub's `0x082D` body was
structurally wrong; the client accepted it (subscribed opcode) and deserialized
garbage → no entity.

**Conclusion:** `0x082D` is the correct handler. The deserializer analysis below
shows the *layout* was actually right — the real defect was an empty roster
(count = 0), now fixed.

## What to try next

1. **Decompile the deserializer** — the base vtable `PTR_LAB_100fa808` "read from
   message" method (and the derived read at `100a5a20`) give the exact field order
   read off the wire. Derive the real layout, rebuild `SpawnZoneUpdate` to match
   (≤ 6132 bytes), and re-test live.
2. Populate a real object/entity entry (80-byte slot) with a valid appearance +
   position rather than zeros, and set the count field.

## Result (2026-07-06)

Ran `FOM_SPAWN_TEST=1` against the live client; logged in and entered the world.
The server confirmably injected the packet ~6s after world entry:

```
[spawn-test] injecting 0x082D clone entity 4243
S->C  0x082D  len=4085  handled=true      (fired 4×, two sessions)
```

Client outcome: **nothing rendered**, no roster row, and no crash/disconnect
attributable to it (the connection kept exchanging PING/POLL normally). So the
packet is well-formed enough to be silently accepted and ignored — the client
needs something more/other to instantiate a visible entity. Black-box injection
has hit its limit here (the stub author reached the same wall).

## Hypothesis

Sending `0x082D` with a single entry — `entity id` (must match the id the entity
uses in `0x03F3` movement), an appearance code, and a name — makes that entity
appear in the recipient's world. If it renders as an avatar, broadcasting a
second player's movement to the same id would animate it (next experiment).

## Packet (SpawnZoneUpdate)

Fixed 4085-byte body: `entity id` (u32) · `appearance` (u32) · 5× reserved u32 ·
`name[52]` · `object count` (u16=0) · pad · 50×80-byte object slots (zeroed) ·
trailing NUL. See `src/FOM.Protocol/Messages/SpawnZoneUpdate.cs`.

## How to run it

```sh
# One service, spawn injection on. Log in and enter the world; ~6s after entry
# the server injects a "CLONE" entity (id = your player id + 4242).
FOM_SPAWN_TEST=1 FOM_CAPTURE=spawn.jsonl just serve
# ... play via Steam, watch for a second character / roster entry ...
```

Then **observe the client**: does a CLONE avatar appear in the world? A row in a
player/roster UI? Nothing? Record the outcome here.


## Reproduce

```sh
# Deterministic packet bytes are golden-tested:
dotnet test FOM.slnx --filter Spawn
# Live experiment (needs the client): FOM_SPAWN_TEST=1 as above.
```
