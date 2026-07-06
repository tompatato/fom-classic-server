# Client runtime patch — localhost redirect + DXVK

Turns a stock 2006 *Face of Mankind* client (`fom_openbeta_v1213`) into one that
connects to a **local** server and renders under **Wine/Vulkan**. Two
independent pieces:

1. **Localhost redirect** — `localhost_patch.py` rewrites the hardcoded server
   address table in `Resources/CShell.dll` to `127.0.0.1`.
2. **DXVK** — a Direct3D 9 → Vulkan translation layer, dropped in as `d3d9.dll`
   next to the game exe, configured by `dxvk.conf`.

> **No game bytes live here.** The patcher edits *your own* staged copy of the
> copyrighted client (see `.gitignore` / `CLAUDE.md`). Only the reproducible
> recipe and the DXVK config are tracked.

## 1. Localhost redirect (`localhost_patch.py`)

### The finding

`CShell.dll` holds the client's server pool as **ten fixed 16-byte slots**, each
a NUL-padded IPv4 dotted-quad string, immediately before the `NETMGRCL`
network-manager marker. The stock build ships the original Duplex Systems
production block:

```
82.133.85.52  .51  .50  .49  .48   82.133.85.46  .45  .44  .43  .42
```
(contiguous `82.133.85.42–52`, `.47` absent)

The patch overwrites every slot with `127.0.0.1\0…`, preserving each slot's
length — so the DLL's total size is unchanged and no PE headers, relocations, or
checksums move. Nothing else in the binary is touched (110 bytes, one region).

The 16-byte slot means **any replacement address is capped at 15 characters**.
`127.0.0.1` fits; a longer hostname would not — that would need a code patch to
the table reader instead.

### Usage

```sh
# preview status without writing
python3 tools/patch/localhost_patch.py --game-dir "$FOMC_GAME_DIR" --check

# apply (keeps a CShell.dll.orig backup)
python3 tools/patch/localhost_patch.py --game-dir "$FOMC_GAME_DIR" --backup

# redirect somewhere other than loopback (<= 15 chars)
python3 tools/patch/localhost_patch.py --game-dir "$FOMC_GAME_DIR" --addr 192.168.1.10
```

The tool matches on the known host strings (not raw offsets, which are
build-specific), warns if the input isn't the pristine v1213 `CShell.dll`, and
— for the default `127.0.0.1` target — reports whether the result byte-matches
the known-good hand patch (`md5 d1dc3fb656eebf9fd6481b3a38aab539`).

Reference hashes (v1213 `CShell.dll`):

| State | md5 |
| --- | --- |
| pristine | `e967cb411970ba7ad12599b00fe6a897` |
| localhost patched | `d1dc3fb656eebf9fd6481b3a38aab539` |

## 2. DXVK

`d3d9.dll` is the **DXVK** build (a third-party, zlib-licensed dependency — *not*
committed here; it's a large prebuilt binary). Fetch a release from
<https://github.com/doitsujin/dxvk/releases> and place its `x32/d3d9.dll` next
to `Lithtech.exe`. The build this recipe was validated against:

```
sha256  00ecd422b3b12e9d3b309785f3f19431405389d80eb4a37018ebc63e2d87d900
size    4493326 bytes
```

`dxvk.conf` (tracked here, copy it next to the exe) sets:

- `d3d9.enableDialogMode = True` — lets the client's Win32 dialogs / message
  boxes render instead of stalling.
- `d3d9.samplerAnisotropy = 16` — 16× anisotropic filtering.

## Applying everything to a staged client

```sh
GAME=/path/to/fom_openbeta_v1213
python3 tools/patch/localhost_patch.py --game-dir "$GAME" --backup
cp tools/patch/dxvk.conf "$GAME"/
cp /path/to/dxvk/x32/d3d9.dll "$GAME"/     # fetched separately
```

> This only redirects and renders the **game shell**. The `FOM.exe` launcher's
> pre-launch version check ("Error while getting file version") is a separate
> problem — bypass it (or launch `Lithtech.exe` directly) as a distinct step.
