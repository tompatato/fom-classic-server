# Face of Mankind (2006) — task runner.
# Phase 0 is reverse-engineering, so the recipes here drive the `fomre` RE harness
# and the headless-Ghidra project. Server build/test recipes get added once servers
# exist. `just` is assumed installed (see docs/toolchain.md).

GHIDRA_DEFAULT := if os_family() == "windows" { 'C:\Program Files (x86)\Ghidra' } else { "/opt/ghidra" }
GHIDRA_HOME := env("GHIDRA_INSTALL_DIR", GHIDRA_DEFAULT)
GHIDRA_PROJECT := "FoMClassic"
# Your own copy of the 2006 client binaries, and which PE modules to import.
GAME_DIR := env("FOMC_GAME_DIR", justfile_directory() / "client")
CLIENT_MODULES := env("FOMC_CLIENT_MODULES", "")

# Optional per-machine recipes (gitignored). Absent on a machine, nothing breaks.
import? 'local.just'

_default:
    @just --list

[group("server")]
[doc('Run the game server (one service, all worlds). Pass CAPTURE=<file> to record a JSONL session.')]
serve CAPTURE="":
    FOM_CAPTURE="{{CAPTURE}}" dotnet run --project src/FOM.Server -c Release

[group("server")]
[doc('Summarize a JSONL capture: traffic, unmapped opcodes, errors.')]
analyze FILE:
    dotnet run --project src/FOM.Server -c Release -- analyze {{FILE}}

[group("server")]
[doc('Build and run all .NET tests.')]
test:
    dotnet test FOM.slnx

[group("re")]
[doc('Run the RE harness CLI (tools/re/fomre.py). e.g. `just re sym Player`')]
re *ARGS:
    python3 tools/re/fomre.py {{ARGS}}

[group("re")]
[doc('Run the RE harness unit tests (static, no game/Ghidra needed).')]
re-test:
    python3 -m unittest discover -s tools/re/tests

[group("client")]
[doc('Redirect the staged client to a local server (patches Resources/CShell.dll in place, keeps a .orig backup). Set FOMC_GAME_DIR, or pass ADDR=<ip>. DXVK d3d9.dll must be fetched separately — see tools/patch/README.md.')]
client-patch ADDR="127.0.0.1":
    python3 tools/patch/localhost_patch.py --game-dir "{{GAME_DIR}}" --addr {{ADDR}} --backup

[group("ghidra")]
[doc('Rebuild the labeled Ghidra project at disassembly/ from committed JSON onto a fresh import of your game binaries. Set FOMC_CLIENT_MODULES + FOMC_GAME_DIR first.')]
ghidra-gen:
    #!/usr/bin/env bash
    set -euo pipefail
    launcher="{{GHIDRA_HOME}}/Ghidra/Features/PyGhidra/support/pyghidra_launcher.py"
    [ -f "$launcher" ] || { echo "Ghidra not found at {{GHIDRA_HOME}}. Set GHIDRA_INSTALL_DIR." >&2; exit 1; }
    modules="$(echo "{{CLIENT_MODULES}}" | tr ',' ' ')"
    [ -n "${modules// /}" ] || { echo "No modules to import. Set FOMC_CLIENT_MODULES (comma-separated) to the 2006 client's PE module filenames — see docs/recon-plan.md Step 0." >&2; exit 1; }
    proj="{{justfile_directory()}}/disassembly"
    [ -e "$proj/{{GHIDRA_PROJECT}}.lock" ] && { echo "{{GHIDRA_PROJECT}} is locked (open in Ghidra, or a stale lock) — close it in Ghidra, or delete $proj/{{GHIDRA_PROJECT}}.lock, then re-run." >&2; exit 1; } || true
    rm -rf "$proj/{{GHIDRA_PROJECT}}".{gpr,rep,lock,lock~}
    for b in $modules; do
        bin="$(find "{{GAME_DIR}}" -name "$b" -type f -print -quit 2>/dev/null || true)"
        if [ -n "$bin" ]; then python3 "$launcher" "{{GHIDRA_HOME}}" --headless "$proj" {{GHIDRA_PROJECT}} -import "$bin" -scriptPath "$proj/scripts" -postScript build_program.py
        else echo "skipping $b (not found under {{GAME_DIR}})"; fi
    done

[group("ghidra")]
[doc('Dump the labeled Ghidra project at disassembly/ back to JSON (run with the project closed in the GUI).')]
ghidra-dump:
    #!/usr/bin/env bash
    set -euo pipefail
    launcher="{{GHIDRA_HOME}}/Ghidra/Features/PyGhidra/support/pyghidra_launcher.py"
    [ -f "$launcher" ] || { echo "Ghidra not found at {{GHIDRA_HOME}}. Set GHIDRA_INSTALL_DIR." >&2; exit 1; }
    proj="{{justfile_directory()}}/disassembly"
    [ -e "$proj/{{GHIDRA_PROJECT}}.lock" ] && { echo "{{GHIDRA_PROJECT}} is locked (open in Ghidra, or a stale lock) — close it in Ghidra, or delete $proj/{{GHIDRA_PROJECT}}.lock, then re-run." >&2; exit 1; } || true
    python3 "$launcher" "{{GHIDRA_HOME}}" --headless "$proj" {{GHIDRA_PROJECT}} -process -noanalysis -scriptPath "$proj/scripts" -postScript export_program.py
