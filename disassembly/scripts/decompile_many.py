# decompile_many.py — decompile several functions in one Ghidra session.
# Args: any number of hex addresses / symbol names. Emits one sentinel-wrapped
# JSON array on stdout.
import json
import sys

from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor

p = currentProgram


def _resolve_function(arg):
    fm = p.getFunctionManager()
    af = p.getAddressFactory()
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
    want = arg.split("::")[-1]
    for fn in fm.getFunctions(True):
        if fn.getName() == want or str(fn.getName()) == arg:
            return fn
    return None


def main():
    args = list(getScriptArgs())
    iface = DecompInterface()
    iface.openProgram(p)
    monitor = ConsoleTaskMonitor()
    results = []
    for target in args:
        fn = _resolve_function(target)
        if fn is None:
            results.append({"target": target, "error": "not found"})
            continue
        res = iface.decompileFunction(fn, 60, monitor)
        out = {
            "target": target,
            "name": str(fn.getName()),
            "entry": "%x" % fn.getEntryPoint().getOffset(),
            "signature": str(fn.getPrototypeString(False, False)),
        }
        if res is not None and res.decompileCompleted():
            out["c"] = res.getDecompiledFunction().getC()
        else:
            out["error"] = "decompile failed"
        results.append(out)
    print("@@FOMRE@@" + json.dumps(results) + "@@END@@")


main()
