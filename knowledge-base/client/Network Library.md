# Network Library

The 2006 client does **not** use RakNet (or any recognizable third-party net
library). It talks a **custom protocol over raw Winsock sockets**: a length-
prefixed **TCP** main channel plus a parallel **UDP** channel for movement. This
resolves recon Step 1 and decides the Step 4 network-layer question — **rewrite,
not fork** FotD's RakNet `FOMNetwork`, since the transports are unrelated. The
wire framing and opcodes are implemented in the `FOM.Protocol` C# library and
documented alongside [[Network Address Table]] and [[Launcher Bootstrap]].

> Status: ✅ verified. Transport/opcodes derived against the 2006 client (via a
> reference server stub that drives the live client through login → world entry),
> and corroborated directly in the client binaries (below). Individual opcode
> *body* layouts beyond the framing are confidence-varying — see the stub / the
> `FOM.Protocol` tests for which are byte-confirmed.

## Flow / Overview

- **TCP** (main channel): one listener per world on `port = 7500 + WorldId`.
  Frame = `[opcode: u16 BE][length: u16 BE][body]`; `length` counts body bytes
  only (≤ 65535).
- **UDP** (same port): movement/position updates (`0x03F3`), sent frequently and
  independently of the TCP channel.
- Endianness is **big-endian** throughout, with one known exception: the
  login-return `pp` field is little-endian.

## Structures

Evidence from the client binaries (`strings`):

| Module | Finding |
| --- | --- |
| `Lithtech.exe`, `CShell.dll`, `server.dll` | import **`WSOCK32.dll`**; `WSAStartup`, `closesocket`, `gethostbyname` |
| `Lithtech.exe` | `udp_BuildSockaddrFromString` — engine's own UDP socket helper |
| all modules | RakNet signature scan (`RakNet`/`RakPeer`/`BitStream`/`ID_CONNECTION_REQUEST`): **0 hits** |

Frame header (TCP):

| Field | Off | Type | Notes |
| --- | --- | --- | --- |
| `opcode` | `0x00` | `u16` BE | see [[[PacketId]]] enum |
| `length` | `0x02` | `u16` BE | body byte count |
| `body`   | `0x04` | … | opcode-specific |

## Findings

- **Not RakNet → rewrite.** FotD's `FOMNetwork` (RakNet 3.611, UDP BitStreams) is
  a different transport entirely; it is *reference only*, not a fork candidate for
  this client. The RakNet fork-vs-reference question from Step 1 is therefore moot.
- **WSOCK32 (Winsock 1.1)**, not `ws2_32` — consistent with a ~2005 build.
- **UDP is a real second channel**, not tunneled over TCP — the engine builds UDP
  sockaddrs directly (`udp_BuildSockaddrFromString`) and the stub captures `0x03F3`
  position packets on the UDP port.
- **Port-per-world**: `7500 + WorldId` (GroundZero → 7500), covering the colony
  range with headroom to ~7545. See [[Network Address Table]] for how the client
  is pointed at these (hardcoded host table → `127.0.0.1`).

## Reproduce

```bash
# RakNet absent across every client module:
for m in Lithtech.exe Resources/CShell.dll Resources/Object.lto server.dll; do
  echo "$m: $(strings -n5 "$FOMC_GAME_DIR/$m" | grep -icE 'RakNet|RakPeer|BitStream')"
done

# Winsock present instead:
strings -n4 "$FOMC_GAME_DIR/Lithtech.exe" | grep -iE 'WSOCK32|WSAStartup|udp_Build'

# Framing + opcode byte-parity is covered by the C# tests:
dotnet test FOM.slnx
```
