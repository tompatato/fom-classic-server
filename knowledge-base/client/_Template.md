# <Topic>

One-paragraph summary: what this note establishes and why it matters. Link the
module/subsystem it lives in with [[Client Architecture]] once that exists.

> Status: 🟡 in progress / ✅ verified against live client / ⚠️ hypothesis. State
> which. If a claim leans on FotD prior art, say so and mark it until confirmed.

## Flow / Overview

Diagram or bullet flow of the interaction (packets, calls, state transitions).

## Structures

| Field | Off | Type | Source / notes |
| --- | --- | --- | --- |
| `example` | `0x00` | `u32` | where the value comes from |

Enums:

- `SomeStatus`: `VALUE_A = 0`, `VALUE_B = 1`, …

## Findings

Prose detail — the reasoning, the surprises, the contradictions with assumptions.
Mark unverified claims explicitly *(unverified)*.

## Reproduce

```bash
# The exact commands that regenerate this finding, e.g.:
# fomre type /FOM/Packets/Packet_ID_...
# fomre decompile <module>:0x<rva>
# fomre struct <module>:0x<addr> /path/to/type
```
