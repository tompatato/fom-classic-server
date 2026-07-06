# Network Address Table

The 2006 client's server addresses are **hardcoded** as a table of ten
fixed-width IPv4 strings inside `CShell.dll` (`.rdata`), immediately before the
`NETMGRCL` marker. This is *how the client reaches its server* (recon Step 2's
first question) and the anchor for the localhost redirect that lets it connect
to a private server. Lives in the network layer alongside [[Network Library]]
and feeds [[Login Handshake]]; see [[Client Architecture]] for the module split.

> Status: ✅ table location, layout, and contents verified byte-for-byte against
> the pristine `CShell.dll` (`md5 e967cb41…`). ⚠️ The table's precise role —
> master/login list vs. world/redirector pool — is **not yet resolved**; the
> `NETMGRCL` adjacency and the fact that redirecting all ten to loopback yields a
> working single-server connect strongly imply these are the addresses the
> client dials, but the master-vs-world distinction awaits [[Login Handshake]].

## Flow / Overview

- Module: `Resources/CShell.dll` (client shell), section `.rdata`.
- Ten consecutive **16-byte slots**, each a NUL-terminated/NUL-padded ASCII
  dotted-quad ("255.255.255.255" + NUL = 16 bytes → the 15-char address cap).
- Table base at file offset `0xDE794`, RVA `0x000DE794` (load VA `0x100DE794`,
  ImageBase `0x10000000`), spanning 160 bytes to `0xDE833`.
- Immediately followed by the ASCII marker `NETMGRCL` (network-manager class).
- Stock contents: the original Duplex Systems block `82.133.85.42–52`, in
  descending order, with `.47` absent.

## Structures

`char server_addr[10][16]` at `.rdata:0x000DE794` — stock values:

| Slot | RVA | File off | Stock value |
| --- | --- | --- | --- |
| 0 | `0x000DE794` | `0xDE794` | `82.133.85.52` |
| 1 | `0x000DE7A4` | `0xDE7A4` | `82.133.85.51` |
| 2 | `0x000DE7B4` | `0xDE7B4` | `82.133.85.50` |
| 3 | `0x000DE7C4` | `0xDE7C4` | `82.133.85.49` |
| 4 | `0x000DE7D4` | `0xDE7D4` | `82.133.85.48` |
| 5 | `0x000DE7E4` | `0xDE7E4` | `82.133.85.46` |
| 6 | `0x000DE7F4` | `0xDE7F4` | `82.133.85.45` |
| 7 | `0x000DE804` | `0xDE804` | `82.133.85.44` |
| 8 | `0x000DE814` | `0xDE814` | `82.133.85.43` |
| 9 | `0x000DE824` | `0xDE824` | `82.133.85.42` |

## Findings

- **Fixed 16-byte slots, no length prefix** — the string is read up to its NUL,
  and any replacement is hard-capped at 15 chars. `127.0.0.1` fits; a longer
  hostname would require patching the code that consumes the table, not the
  table itself.
- **`.rdata` (read-only)** — the table is const data, not writable globals, so a
  redirect is a static binary edit; the client does not appear to rewrite it at
  runtime from a config file *(unverified — no config-load path traced yet)*.
- **Redirect is length-preserving** — overwriting the ASCII in place (NUL-filling
  the tail of each slot) keeps the DLL byte length identical, so no PE headers,
  relocations, or checksums move. This is exactly what the tracked
  `tools/patch/localhost_patch.py` recipe does; it reproduces the community hand
  patch byte-for-byte (`md5 d1dc3fb6…`).
- **`.47` gap** — the stock block skips `82.133.85.47`; reason unknown (retired
  host? intentional?). Recorded as an observation, not a conclusion.
- **Open question** — are all ten peers of one role, or is slot 0 a master and
  the rest world/backup servers? Redirecting *all* to loopback sidesteps the
  question for now; resolving it belongs with [[Login Handshake]].

## Reproduce

```bash
# Inspect / confirm the stock table and offsets (pristine CShell.dll):
python3 tools/patch/localhost_patch.py --game-dir "$FOMC_GAME_DIR" --check

# Map the table's file offset to an RVA via the PE section headers:
python3 - <<'PY'
import struct
d=open("$FOMC_GAME_DIR/Resources/CShell.dll","rb").read()
e=struct.unpack_from("<I",d,0x3c)[0]; opt=struct.unpack_from("<H",d,e+20)[0]
imgbase=struct.unpack_from("<I",d,e+24+28)[0]; nsec=struct.unpack_from("<H",d,e+6)[0]
foff=0xDE794
for i in range(nsec):
    o=e+24+opt+i*40; name=d[o:o+8].rstrip(b"\0").decode()
    vsz,va,rsz,praw=struct.unpack_from("<IIII",d,o+8)
    if praw<=foff<praw+rsz: print(name, hex(foff-praw+va), hex(imgbase+foff-praw+va))
PY

# Show the raw table region as text:
dd if="$FOMC_GAME_DIR/Resources/CShell.dll" bs=1 skip=911252 count=160 2>/dev/null | tr -c '[:print:]' '.'
```
