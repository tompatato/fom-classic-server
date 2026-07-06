# Launcher Bootstrap

How the 2006 client starts: `FOM.exe` is a **patcher/launcher** that version-checks
game files against the (now-dead) patch server and, on success, spawns the engine
`Lithtech.exe` with a fixed command line. Bypassing `FOM.exe` and running the
engine directly is what makes the client launchable today. Operational steps live
in [[Running the Client]] (`docs/running-the-client.md`); this note records the
binary facts. Related: [[Network Address Table]], and the engine module split in
[[Client Architecture]] (TBD).

> Status: ✅ verified — arg string read from `FOM.exe`; launch confirmed rendering
> to the login screen under Proton; crash address captured from a live Wine fault.

## Flow / Overview

1. `FOM.exe` (the window titled *"Face of Mankind — Launcher"*) fetches a remote
   version file (`launcherversion.txt` / `version.txt`), compares it to the local
   file version, and shows **"Error while getting file version…"** when the patch
   host is unreachable (all of `82.133.85.42–52` are dead — see
   [[Network Address Table]]).
2. On success it launches the engine:
   `LithTech.exe -windowtitle "Face of Mankind" -rez Resources -dpsmagic 1`,
   appending `+windowed 1` when the launcher's *Window Mode* box is ticked.
3. `Lithtech.exe` loads the shell/content modules (`cshell.dll`, `cres.dll`,
   `Object.lto`, `dtype*.dll`) and initializes sound, input (DINPUT8), and D3D9.
4. **Bypass:** skip step 1 entirely — run `Lithtech.exe` with the step-2 args.

## Structures

Strings in `FOM.exe` (`ImageBase 0x400000`):

| String | File off | Role |
| --- | --- | --- |
| `-windowtitle "Face of Mankind" -rez Resources -dpsmagic 1` | `0x432BD` | Args passed to the engine |
| `lithtech.exe` | `0x4324C` | Engine image the launcher spawns |
| `launcherversion.txt` | `0x43618` | Remote version file it fetches |
| `Get remote file version from File:` | `0x431F8` | Version-check code path |

## Findings

- **`-rez Resources` is mandatory.** It points the engine at the content tree; a
  bare `Lithtech.exe` launch quits immediately. This is the single most common
  reason a Steam shortcut "does nothing".
- **`windowed` / `ScreenWidth` / `ScreenHeight` are console vars** (set with `+`),
  not engine flags. `-windowtitle` / `-rez` / `-dpsmagic` are flags (set with `-`).
- **Sound-init null-deref without an audio device.** When no audio backend is
  available, `ILTSoundSys` reports *"Failed to initialize sound driver
  'SndDrv.dll'"* and the engine then faults reading `0x00000000` at
  **`lithtech+0x9CC9D`** (`mov (%eax),%edx`, `EAX=0`) — the null sound device used
  unchecked. Giving Wine a working device (Proton's forwarded PipeWire) avoids it;
  `+ForceNoSound 1` alone does **not** (device enumeration still runs). *(The exact
  guard/callsite is not yet decompiled — candidate for a Ghidra look.)*
- **Module load set** (from the fault's module list): engine `lithtech`, render
  `dtypelay/dtypestd/dtypepwr`, `d3d9` (DXVK) + `d3dx9_26`, `cshell`, `cres`,
  `snddrv`, `ltmsg`; plus Wine `dinput8`, `dsound`, `winevulkan`, `ws2_32`,
  `msvcr71/msvcp71`. Confirms the VS2003 (MSVC 7.1) toolchain.

## Reproduce

```sh
# Read the spawn args baked into the launcher:
python3 - <<'PY'
d=open("$FOMC_GAME_DIR/FOM.exe","rb").read()
i=d.find(b'-windowtitle'); print(repr(d[i-24:i+52]))
PY

# Launch the engine directly (bypassing FOM.exe) — see docs/running-the-client.md:
#   Proton:  python3 "<proton>/proton" run Lithtech.exe +windowed 1 \
#            -windowtitle "Face of Mankind" -rez Resources -dpsmagic 1
```
