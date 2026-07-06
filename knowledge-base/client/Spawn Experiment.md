# Spawn Experiment (0x082D)

Can we make **another player visible** in the client by sending a `0x082D` zone
update carrying one player entry? This is the gating unknown for multiplayer
visibility. The packet is implemented (`SpawnZoneUpdate`) and byte-pinned to the
reference stub, and the server can inject it after world entry — but whether the
client renders a **3D avatar** (vs. only a UI roster row) is **not yet confirmed**.
Related: [[Session Opcodes]], [[Network Library]].

> Status: ⚠️ **UNVERIFIED / experimental.** The reference stub disabled this
> (`SPAWN_TEST = False`) with the note "0x82D populates a UI roster, not a 3D
> avatar." We're re-testing it against the live client with the harness.

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

## What to try next (by outcome)

- **Avatar appears** → broadcast a mover's `0x03F3` to the clone id to animate it;
  then generalize to real per-world player broadcast.
- **Only a UI roster / nothing** → the real spawn is a different opcode or a
  different `0x082D` shape; diff a *real* two-client session capture (once we can
  run two clients) against what we send, and adjust the reserved fields / object
  array.

## Reproduce

```sh
# Deterministic packet bytes are golden-tested:
dotnet test FOM.slnx --filter Spawn
# Live experiment (needs the client): FOM_SPAWN_TEST=1 as above.
```
