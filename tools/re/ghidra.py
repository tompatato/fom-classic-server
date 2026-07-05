"""Headless Ghidra bridge for fomre.

Runs PyGhidra headless against the committed `disassembly/FoMClassic` project
(built by `just ghidra-gen`) to decompile functions and list xrefs on demand. The
Ghidra-side scripts live in `disassembly/scripts/{decompile,xref}.py` and emit a
single sentinel-wrapped JSON object so we can recover it from Ghidra's noisy
stdout.

Ghidra location resolves from `GHIDRA_INSTALL_DIR` (falling back to common
paths); the JDK from `JAVA_HOME` or `FOMC_GHIDRA_JDK`. If neither Ghidra nor the
built project is present, raises `GhidraUnavailable` with guidance — the rest of
fomre (static DB + live memory) works without any of this.
"""

from __future__ import annotations

import fcntl
import json
import os
import re
import subprocess
import tempfile
from contextlib import contextmanager
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parents[2]
_PROJECT_DIR = _REPO_ROOT / "disassembly"
_PROJECT_NAME = "FoMClassic"
_SCRIPTS = _PROJECT_DIR / "scripts"
_SENTINEL = re.compile(r"@@FOMRE@@(.*?)@@END@@", re.S)
# Machine-specific paths (this install's Ghidra/JDK) live here, gitignored.
_LOCAL_CONFIG = Path(__file__).resolve().parent / "ghidra.local.json"


class GhidraUnavailable(RuntimeError):
    pass


def _local() -> dict:
    if _LOCAL_CONFIG.is_file():
        try:
            return json.loads(_LOCAL_CONFIG.read_text())
        except json.JSONDecodeError:
            pass
    return {}


def extract_result(blob: str) -> dict | None:
    """Pull the sentinel-wrapped JSON object out of Ghidra's noisy output."""
    m = _SENTINEL.search(blob)
    return json.loads(m.group(1)) if m else None


def _ghidra_home() -> Path:
    candidates = [
        os.environ.get("GHIDRA_INSTALL_DIR"),
        _local().get("install_dir"),
        "/opt/ghidra",
    ]
    for c in candidates:
        if c and (Path(c) / "support" / "analyzeHeadless").is_file():
            return Path(c)
    raise GhidraUnavailable(
        "Ghidra not found. Set GHIDRA_INSTALL_DIR (or tools/re/ghidra.local.json "
        '{"install_dir": ...}) to a Ghidra 12.0.4 install.'
    )


def _launcher(home: Path) -> Path:
    return home / "Ghidra" / "Features" / "PyGhidra" / "support" / "pyghidra_launcher.py"


def project_built() -> bool:
    return (_PROJECT_DIR / f"{_PROJECT_NAME}.gpr").is_file()


# Headless Ghidra holds an exclusive lock on the project, so only one analysis
# can run at a time. Serialize across processes (e.g. parallel agents) with a
# cross-process file lock — callers queue instead of failing on a locked project.
_LOCK_PATH = Path(tempfile.gettempdir()) / "fomre-ghidra-FoMClassic.lock"


@contextmanager
def _project_lock(timeout: int):
    f = open(_LOCK_PATH, "w")
    try:
        fcntl.flock(f, fcntl.LOCK_EX)  # blocks until the project is free
        yield
    finally:
        fcntl.flock(f, fcntl.LOCK_UN)
        f.close()


def _env() -> dict:
    env = dict(os.environ)
    # Ghidra 12.0.4 requires a JDK in [21, 24]; this host's default may be newer.
    jdk = env.get("FOMC_GHIDRA_JDK") or env.get("JAVA_HOME") or _local().get("jdk")
    if jdk:
        env["JAVA_HOME"] = jdk
    return env


def _run(program: str, script: str, args: list[str], timeout: int = 300) -> dict:
    home = _ghidra_home()
    if not project_built():
        raise GhidraUnavailable(
            f"Ghidra project not built ({_PROJECT_DIR}/{_PROJECT_NAME}.gpr missing). "
            "Run `just ghidra-gen` first."
        )
    launcher = _launcher(home)
    cmd = [
        "python3", str(launcher), str(home), "--headless",
        str(_PROJECT_DIR), _PROJECT_NAME,
        "-process", program, "-noanalysis", "-readOnly",
        "-scriptPath", str(_SCRIPTS), "-postScript", script, *args,
    ]
    # Serialize concurrent callers; allow extra wall-clock for queueing behind
    # another in-flight analysis.
    with _project_lock(timeout):
        proc = subprocess.run(cmd, capture_output=True, text=True, env=_env(),
                              timeout=timeout)
    blob = "\n".join([proc.stdout or "", proc.stderr or ""])
    result = extract_result(blob)
    if result is None:
        tail = (proc.stdout or proc.stderr or "")[-800:]
        raise GhidraUnavailable(
            f"no result from Ghidra script {script} (exit {proc.returncode}).\n--- tail ---\n{tail}"
        )
    return result


def decompile(program: str, target: str, timeout: int = 300) -> dict:
    """Decompile a function (by hex address or name) in `program` to C."""
    return _run(program, "decompile.py", [target], timeout)


def xref(program: str, target: str, direction: str = "to", timeout: int = 300) -> dict:
    """List references to (default) or from a function/address in `program`."""
    return _run(program, "xref.py", [target, direction], timeout)
