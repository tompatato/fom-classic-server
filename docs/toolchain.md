# Toolchain

The RE toolchain (Ghidra + a JDK + a Python for PyGhidra) is **not** part of this
repo — install it wherever you like and point the harness at it via environment
variables or a gitignored local config. What matters is the **version
constraints** below; the host defaults on a modern distro are often too new and
fail in non-obvious ways.

## Task runner (`just`)

Build, RE, and dev workflows are driven by [`just`](https://just.systems) recipes,
following the same convention as the FotD project. **`just` is assumed to be on
your `PATH`** — install it once:

- Fedora: `sudo dnf install just`
- Or `cargo install just`, or a prebuilt binary into `~/.local/bin`.

No `justfile` exists yet — Phase 0 is recon and needs neither a build nor the
recipe set. Once the RE harness is ported and servers are scaffolded, their recipes
live in a root `justfile` and are invoked as `just <recipe>` (e.g. `just re-test`,
`just ghidra-gen`).

## Version constraints

| Tool | Required version | Why the newest isn't fine |
| --- | --- | --- |
| Ghidra | 12.0.4 (match whatever the `disassembly/` export is pinned to) | Its analysis/export format is version-sensitive. |
| JDK | **21–24** (e.g. Temurin 21) | Ghidra 12.0.4 rejects JDK 25+ with "unsupported java version". |
| CPython | **3.10–3.13** | PyGhidra's bundled `jpype` wheels stop at cp313; 3.14+ has no wheel and can't build from sdist without a compiler. |

## Pointing the harness at your install

Resolution order (mirrors the FotD `fomre` harness):

- **Ghidra**: `$GHIDRA_INSTALL_DIR` → a gitignored `tools/re/ghidra.local.json`
  `install_dir` → a conventional fallback.
- **JDK**: `$FOTD_GHIDRA_JDK` / `$JAVA_HOME` → `ghidra.local.json` `jdk`. Make sure
  this resolves to a 21–24 JDK, not a newer host default.
- **Python**: PyGhidra installs into a Ghidra-managed venv on first run. If the
  default `python3` is unsupported, create that venv from a 3.10–3.13 interpreter
  and the launcher reuses it.

`tools/re/ghidra.local.json` is per-machine and **gitignored** — never commit
absolute install paths. Shape:

```json
{ "install_dir": "/path/to/ghidra_12.0.4_PUBLIC", "jdk": "/path/to/jdk-21" }
```

## Live process memory (ptrace)

Reading the client's memory needs:

- the harness running as the **same UID** as the client process, **and**
- `kernel.yama.ptrace_scope = 0`, or running the harness as the client's parent.

On `EACCES`/`EPERM`: `sudo sysctl kernel.yama.ptrace_scope=0`.

## SELinux + Docker (relevant once servers exist)

On SELinux-enforcing distros (e.g. Fedora), SELinux blocks Docker build
bind-mounts (`--mount type=bind` → `Permission denied`), which breaks any `just`
recipe that mounts the source tree into a build container. When this project adds
Docker build recipes, bake the SELinux-safe mount **into the `justfile` itself** —
`,relabel=shared` on the bind mount (or `--security-opt label=disable` on the
`docker run`) — so `just <recipe>` works on enforcing hosts without a separate
wrapper script. Named volumes are unaffected. Not relevant during Phase 0 recon,
which needs neither Docker nor a native build.

## Client runtime

The 2006 client predates Steam, so the FotD Steam-Proton shortcut approach likely
doesn't apply — expect a plain Wine prefix. Watch for a **keyboard/OEM-key gotcha**
seen with the FotD client: under KDE Wayland, XWayland can present layout `us` while
a UK keyboard is `gb`, so `- = / \ ; ' @` produce no character in the client's text
fields. Fix: set the matching layout (e.g. `setxkbmap gb`) before launch — Wine
reads the layout at startup — or use an X11 session.
