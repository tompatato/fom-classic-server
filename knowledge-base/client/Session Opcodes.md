# Session Opcodes

The packet flow from login through world entry, and the keepalive/ack opcodes the
client emits. Verified against a **live capture** of the 2006 client talking to
the C# server (`FOM.Server`) over the [[Network Library]] TCP channel — the client
logged in, loaded a character, entered a world, and walked around. Opcodes are in
[[[PacketId]]]; the framing is in [[Network Library]].

> Status: ✅ the login→world flow and the four keepalive/ack bodies below are
> confirmed against a live client session. `0x083B` (~30s keepalive) is still
> ⚠️ *unconfirmed* — it did not appear in the (short) capture.

## Flow / Overview

Observed C→S / S→C order for a successful entry (single connection, one TCP port
= the world; movement on the matching UDP port):

```
C->S 0x07D1 LOGIN_REQUEST   ->  S->C 0x07D2 LOGIN_RETURN
C->S 0x07DC LOAD_CHAR        ->  S->C 0x03EB ENTER_WORLD
C->S 0x081C EXIT_APT         ->  S->C 0x03EB ENTER_WORLD
C->S 0x0809 POST_ENTER       (after apartment entry)
C->S 0x081A WORLD_LOADED     (ack after ENTER_WORLD)
C->S 0x03E9 NODE_REQ         (after colony entry)
C->S 0x0822 POLL             (periodic keepalive, frequent)
C<->S 0x07E5 PING            (keepalive, echoed)
UDP  0x03F3 MOVEMENT         (position/heading while moving)
```

## Structures

Keepalive / ack bodies (big-endian), confirmed from the capture:

| Opcode | Name | Body | Notes |
| --- | --- | --- | --- |
| `0x0822` | POLL | `00 00` | u16 = 0; pre-login + periodic |
| `0x0809` | POST_ENTER | `00 00` | u16 = 0; one-shot after apartment |
| `0x081A` | WORLD_LOADED | `00 00 00 05  00 00 00 00` | u32 = 5, u32 = 0 |
| `0x03E9` | NODE_REQ | `00 00 00 4C` | u32 = 76 |

## Findings

- **These four are fire-and-forget.** The server sent no response to any of them
  and the client still logged in, entered the world, and moved — so they need no
  handler for basic function. They were "UNMAPPED" in the analyzer only because we
  choose not to reply, not because they broke anything.
- **Bodies match the reference stub's tentative annotations exactly** (NODE_REQ
  u32=76, WORLD_LOADED u32=5) — corroborating the opcode map against the live 2006
  client, so those labels graduate from `?` to confirmed in `PacketId`.
- **Movement (`0x03F3`) is validated on live data**: once walking, datagrams
  decode with `session = 12345` (the login-return id) and real X/Y/Z/heading.
- **World routing gap**: the client connected on Aquatica (port 7528) while the
  server's stub `ENTER_WORLD` hardcodes StsGenesis — the world the client actually
  uses is its own; server-side world/session state is still stubbed (next work).

## Reproduce

```bash
# Capture a live session, then summarize it:
FOM_BIND=127.0.0.1 FOM_CAPTURE=session.jsonl \
  dotnet src/FOM.Server/bin/Release/net10.0/FOM.Server.dll     # then log in via the client
dotnet src/FOM.Server/bin/Release/net10.0/FOM.Server.dll analyze session.jsonl
# or the whole thing: just live-check DURATION=120
```
