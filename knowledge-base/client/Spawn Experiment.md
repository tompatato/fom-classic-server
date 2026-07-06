# Spawn Experiment (0x082D)

Can we make **another player visible** in the client by sending a `0x082D` zone
update carrying one player entry? This is the gating unknown for multiplayer
visibility. The packet is implemented (`SpawnZoneUpdate`) and byte-pinned to the
reference stub, and the server can inject it after world entry — but whether the
client renders a **3D avatar** (vs. only a UI roster row) is **not yet confirmed**.
Related: [[Session Opcodes]], [[Network Library]].

> Status: ❌ **Disproven as a standalone spawn (2026-07-06).** Sending a single-entry
> `0x082D` to the live client produced **no visible effect** — no 3D avatar and no
> UI roster entry (see Result). So `0x082D`-with-one-entry is not the avatar-spawn,
> or is insufficient on its own. Matches the reference stub's disabling of it.

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

## What to try next (given "nothing rendered")

Cheapest → most rigorous:

1. **Spawn + movement together** — inject `0x082D`, *then* send `0x03F3` movement
   for the same entity id back to the client's UDP source (the stub prototyped both
   halves separately, never together). Quick code change; may be what "instantiates"
   the entity.
2. **Static RE (Ghidra)** — decompile the client's `0x082D` handler in
   `CShell.dll`/`Object.lto` to see exactly which fields it reads and what path
   actually creates a world entity/avatar. Definitive; this is what `disassembly/`
   exists for. Likely reveals the real spawn opcode/struct.
3. **Live memory** — watch the client's entity list while injecting variants
   (`fomre` read/scan) to see if anything lands.

## Reproduce

```sh
# Deterministic packet bytes are golden-tested:
dotnet test FOM.slnx --filter Spawn
# Live experiment (needs the client): FOM_SPAWN_TEST=1 as above.
```
