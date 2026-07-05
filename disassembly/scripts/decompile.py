# decompile.py — decompile one function to C, emitted as JSON on stdout.
#
# Headless:
#   analyzeHeadless <proj> FoMClassic -process <program> -noanalysis \
#       -scriptPath disassembly/scripts -postScript decompile.py <addr-or-name>
#
# Argument: a hex address (e.g. 0x00401230 / 401230) in the program's image,
# or a symbol name / Namespace::Name. Output is a single JSON object so callers
# (fomre.py) can parse it; everything else the script prints goes to stderr.
import json
import sys

from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor

EXPECTED_GHIDRA_VERSION = "12.0.4"

p = currentProgram


def _check_version():
    from ghidra.framework import Application
    running = str(Application.getApplicationVersion())
    if running != EXPECTED_GHIDRA_VERSION:
        raise RuntimeError("GHIDRA VERSION MISMATCH — pinned to %s but running %s."
                           % (EXPECTED_GHIDRA_VERSION, running))


def _err(msg):
    sys.stderr.write(msg + "\n")


def _resolve_function(arg):
    fm = p.getFunctionManager()
    af = p.getAddressFactory()
    # try as an address first
    token = arg.lower()
    if token.startswith("0x"):
        token = token[2:]
    try:
        addr = af.getAddress(token)
        if addr is not None:
            fn = fm.getFunctionContaining(addr)
            if fn is not None:
                return fn
    except Exception:
        pass
    # fall back to symbol name (last path component matches the function name)
    want = arg.split("::")[-1]
    for fn in fm.getFunctions(True):
        if fn.getName() == want or str(fn.getName()) == arg:
            return fn
    return None


def main():
    _check_version()
    args = list(getScriptArgs())
    if not args:
        print(json.dumps({"error": "no address/name argument"}))
        return
    target = args[0]
    fn = _resolve_function(target)
    if fn is None:
        print(json.dumps({"error": "function not found: %s" % target}))
        return

    iface = DecompInterface()
    iface.openProgram(p)
    monitor = ConsoleTaskMonitor()
    res = iface.decompileFunction(fn, 60, monitor)
    out = {
        "program": str(p.getName()),
        "name": str(fn.getName()),
        "namespace": str(fn.getParentNamespace().getName(True))
        if fn.getParentNamespace() else "",
        "entry": "%x" % fn.getEntryPoint().getOffset(),
        "signature": str(fn.getPrototypeString(False, False)),
    }
    if res is not None and res.decompileCompleted():
        out["c"] = res.getDecompiledFunction().getC()
    else:
        out["error"] = "decompile failed: %s" % (
            res.getErrorMessage() if res is not None else "no result")
    # Sentinel-wrapped so callers can extract the JSON from Ghidra's noisy stdout.
    print("@@FOMRE@@" + json.dumps(out) + "@@END@@")


main()
