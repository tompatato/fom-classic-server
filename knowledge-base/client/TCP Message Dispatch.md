# TCP Message Dispatch

The client's master opcode→handler map, from the TCP dispatch router
`CShell.dll!FUN_10079840`. It reads the opcode (u16 at message `+4`), logs
*"TCP message received, ID %u, %u bytes"*, and switches to a per-opcode handler.
Handlers are grouped by the subsystem object they run on. Complements
[[Session Opcodes]] and [[Network Library]].

> Status: ✅ verified — decompiled from `FUN_10079840` (Ghidra). Handler *names*
> are addresses (unlabeled); subsystem grouping is by the global they dispatch on.

> **This is only the CShell half.** `Object.lto` (the LithTech local server) has a
> *second, disjoint* TCP router — ServerShell `OnMessage` (`FUN_100371d0`) — that
> owns the world range `0x3EA`–`0x3FF`, `0x7D2`, and `0x7D7`–`0x831`, and is where
> the actual avatar spawn lives. See [[World Object Spawn]]. This CShell router
> (`0x7DE`–`0x83D`) handles UI/roster/economy only.

## Subsystems

- **`DAT_1010ced8`** — player/roster manager. Owns `0x82d` (full roster → a
  1000-slot table keyed by id at `+0x1eff0`; see [[Spawn Experiment]]) and its
  per-player siblings. **Most likely home of per-player add/remove.**
- **`DAT_1010ced4`** — a separate subsystem (`0x7ee`–`0x80d`). **Top candidate for
  world/object (character-model) creation** — distinct from the roster data.
- **`DAT_1010cea0`** — `0x808` only.
- **standalone** — handlers taking just the message (no subsystem object).

## Handler map

| Opcode | Handler | Subsystem |
| --- | --- | --- |
| `0x7de` | `FUN_10076680` | ced8 |
| `0x7e2`/`0x7e3`/`0x7e4` | `FUN_100772a0`/`FUN_10077520`/`FUN_10074a20` | ced8 |
| `0x7e5` PING | `FUN_10079580` | (shell) |
| `0x7ee` | `FUN_10063ec0` | ced4 |
| `0x7f0`/`0x7f1` | `FUN_10083260`/`FUN_10084b50` | standalone |
| `0x7f3`/`0x7f7`/`0x7f9`/`0x7fc`/`0x7fe`/`0x800`/`0x803`/`0x806`/`0x80a`/`0x80d` | `FUN_10066450` … `FUN_100659e0` | ced4 |
| `0x808` | `FUN_1007cab0` | cea0 |
| `0x811`/`0x812` | `FUN_100748b0`/`FUN_10076f20` | ced8 |
| `0x817` | `FUN_10074470` | ced8 |
| `0x819` | `FUN_10073be0` | ced8 |
| `0x81a` WORLD_LOADED | `FUN_10070ce0` | standalone |
| `0x81c`/`0x81e`/`0x81f`/`0x820`/`0x821`/`0x827` | `FUN_10085870` … `FUN_10070a60` | standalone |
| `0x825`/`0x826`/`0x828` | `FUN_10076170`/`FUN_10074be0`/`FUN_10077730` | ced8 |
| `0x82d` ROSTER | `FUN_10074590` | ced8 |
| `0x82f`/`0x833`/`0x837`/`0x83a` | `FUN_10074700`/`FUN_10074d20`/`FUN_10073af0`/`FUN_100742d0` | ced8 |
| `0x834` | `FUN_10073910` | standalone |
| `0x83d` | `FUN_10086630` | standalone |

## Findings

- `0x82d` is the full-roster update; the **per-player** opcodes on the same
  manager (`0x82f`, `0x817`, `0x819`, `0x825`, `0x826`, `0x837`, `0x83a`) are the
  natural place for "player entered / left" — one of these likely triggers the
  actual character-model creation, or the `ced4` subsystem does.
- Registration of these opcodes is `FUN_100792a0` (see [[Session Opcodes]]);
  dispatch is this function.

## Reproduce

```bash
bash tools/…/gdecomp.sh 0x10079840   # the switch
bash tools/…/gxref.sh   0x100XXXXX to # who calls a handler
```
