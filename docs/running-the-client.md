# Running the 2006 Client

The client launches, renders under Vulkan, and reaches the login screen. It is
run by driving the engine (`Lithtech.exe`) directly under **Proton Experimental**
via a Steam non-Steam shortcut — the `FOM.exe` launcher is bypassed. The
mechanism (launcher version-check, spawn args, sound-init crash) is documented in
[`knowledge-base/client/Launcher Bootstrap.md`](../knowledge-base/client/Launcher%20Bootstrap.md).

## TL;DR

- **Target:** `<client>/Lithtech.exe` (your staged `fom_openbeta_v1213/`), **not** `FOM.exe`.
- **Compatibility:** force **Proton Experimental**.
- **Launch options:**
  ```
  %command% +windowed 1 -windowtitle "Face of Mankind" -rez Resources -dpsmagic 1 +ScreenWidth 1920 +ScreenHeight 1080
  ```

## Prerequisites (one-time)

1. **Localhost + DXVK patch applied** — see [`tools/patch/`](../tools/patch/README.md):
   patched `CShell.dll` (server table → `127.0.0.1`), DXVK `d3d9.dll`, `dxvk.conf`.
2. **`d3dx9_26.dll` next to the exe** — DXVK provides `d3d9` but not the D3DX
   helper the engine imports. Extract it from the client's own bundled cab:
   ```sh
   cabextract -F d3dx9_26.dll -d "$FOMC_GAME_DIR" \
     "$FOMC_GAME_DIR/DirectX9c_Update/Jun2005_d3dx9_26_x86.cab"
   ```

## Why Proton, not raw Wine

A fresh Fedora `wine-staging` prefix has **no working audio backend**
(`err:mmdevapi:init_driver No driver from "pulse,alsa,oss,coreaudio" could be
initialized`). The engine's `CSoundMgr::InitSound` then dereferences the null
sound device and crashes (`lithtech+0x9CC9D`, read of `0x0`). Proton bundles its
own `winepulse` + a pressure-vessel container that forwards the host PipeWire, so
sound initializes and the crash never happens. Proton also ships DXVK. Raw Wine
*can* work if sound is forced off, but Proton is the path of least resistance and
matches how the sibling FotD client is run.

## Steam setup (recommended)

1. Add `<client>/Lithtech.exe` as a non-Steam game.
2. Properties → **Compatibility** → *Force the use of a specific Steam Play
   compatibility tool* → **Proton Experimental**.
3. Properties → **Launch Options** → paste the string from the TL;DR.
4. Leave **Start In** as the client directory (default). Steam mounts it into the
   pressure-vessel container automatically, even though it lives outside the Steam
   library — no `STEAM_COMPAT_MOUNTS` needed via the GUI.
5. **Play** → the login screen renders.

Argument reference:

| Arg | Meaning |
| --- | --- |
| `+windowed 1` | Windowed mode. Console var — note the **`+`**, not `-`. Omit for fullscreen. |
| `+ScreenWidth N` / `+ScreenHeight N` | Window/render size. Default (from `autoexec.cfg`): 1024×768. |
| `-windowtitle "…"` | Engine flag; window caption. |
| `-rez Resources` | **Required** — points the engine at the game content tree. Missing this is why a bare launch silently fails. |
| `-dpsmagic 1` | Engine flag passed verbatim by the stock launcher. |

## CLI / headless (for automated testing & handshake capture)

Useful for driving the client without the Steam GUI (e.g. to trigger the login
handshake against a local listener):

```sh
STEAMROOT=~/.local/share/Steam
PROTON="$STEAMROOT/steamapps/common/Proton - Experimental/proton"
export STEAM_COMPAT_DATA_PATH=<a compatdata dir outside the repo>
export STEAM_COMPAT_CLIENT_INSTALL_PATH="$STEAMROOT"
export PULSE_SERVER="unix:${XDG_RUNTIME_DIR:-/run/user/$(id -u)}/pulse/native"
export DISPLAY=:0
cd "$FOMC_GAME_DIR"
python3 "$PROTON" run Lithtech.exe +windowed 1 -windowtitle "Face of Mankind" \
  -rez Resources -dpsmagic 1
```

First run builds the prefix (~30 s). Set `PROTON_LOG=1` for a detailed
`steam-*.log` in `$HOME`.

## Expected result

The login screen renders and shows **"All servers are unavailable."** This is
**expected and confirms the localhost patch is live**: the client is dialing the
addresses we rewrote to `127.0.0.1` and finding nothing listening (no server
exists yet). Had the patch not taken, it would instead hang against the dead
`82.133.85.x` block. This screen is the trigger point for the login-handshake
capture (recon Step 2).

## Troubleshooting

- **Steam launch does nothing / instant exit** → empty Launch Options, i.e. no
  `-rez Resources`. The engine has no content to load and quits.
- **Won't go windowed** → use `+windowed 1` (plus/console-var), not `-windowed 1`.
- **Raw-Wine crash at `lithtech+0x9CC9D`** → no audio device; run under Proton.
- **UK OEM keys don't type** (`- = / \ ; ' @`) under XWayland → `setxkbmap gb`
  before launch, or use an X11 session (see [`toolchain.md`](toolchain.md)).
