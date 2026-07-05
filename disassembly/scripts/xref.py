# xref.py — list cross-references to/from an address or function, as JSON.
#
# Headless:
#   analyzeHeadless <proj> FoMClassic -process <program> -noanalysis \
#       -scriptPath disassembly/scripts -postScript xref.py <addr-or-name> [to|from]
#
# Default direction is "to" (who references the target). "from" lists what the
# target's body references (calls + data). Output is a single JSON object.
import json
import sys

from ghidra.program.model.symbol import RefType

EXPECTED_GHIDRA_VERSION = "12.0.4"

p = currentProgram


def _check_version():
    from ghidra.framework import Application
    running = str(Application.getApplicationVersion())
    if running != EXPECTED_GHIDRA_VERSION:
        raise RuntimeError("GHIDRA VERSION MISMATCH — pinned to %s but running %s."
                           % (EXPECTED_GHIDRA_VERSION, running))


def _symbol_at(addr):
    s = p.getSymbolTable().getPrimarySymbol(addr)
    return str(s.getName(True)) if s is not None else None


def _func_name_at(addr):
    fn = p.getFunctionManager().getFunctionContaining(addr)
    return str(fn.getName(True)) if fn is not None else None


def _resolve_addr(arg):
    af = p.getAddressFactory()
    fm = p.getFunctionManager()
    token = arg[2:] if arg.lower().startswith("0x") else arg
    try:
        addr = af.getAddress(token)
        if addr is not None and p.getMemory().contains(addr):
            return addr
    except Exception:
        pass
    want = arg.split("::")[-1]
    for fn in fm.getFunctions(True):
        if fn.getName() == want or str(fn.getName()) == arg:
            return fn.getEntryPoint()
    for s in p.getSymbolTable().getAllSymbols(True):
        if s.getName() == want or str(s.getName(True)) == arg:
            return s.getAddress()
    return None


def main():
    _check_version()
    args = list(getScriptArgs())
    if not args:
        print(json.dumps({"error": "no address/name argument"}))
        return
    target = args[0]
    direction = (args[1] if len(args) > 1 else "to").lower()
    addr = _resolve_addr(target)
    if addr is None:
        print(json.dumps({"error": "address/symbol not found: %s" % target}))
        return

    rm = p.getReferenceManager()
    out = {
        "program": str(p.getName()),
        "target": "%x" % addr.getOffset(),
        "symbol": _symbol_at(addr),
        "direction": direction,
        "refs": [],
    }
    if direction == "from":
        fn = p.getFunctionManager().getFunctionContaining(addr)
        body = fn.getBody() if fn is not None else None
        it = (rm.getReferenceSourceIterator(body.getMinAddress(), True)
              if body is not None else rm.getReferenceSourceIterator(addr, True))
        seen = set()
        for src in it:
            if body is not None and not body.contains(src):
                break
            for r in rm.getReferencesFrom(src):
                to = r.getToAddress()
                key = (src.getOffset(), to.getOffset())
                if key in seen:
                    continue
                seen.add(key)
                out["refs"].append({
                    "from": "%x" % src.getOffset(),
                    "to": "%x" % to.getOffset(),
                    "type": str(r.getReferenceType()),
                    "toSymbol": _symbol_at(to) or _func_name_at(to),
                })
    else:
        for r in rm.getReferencesTo(addr):
            frm = r.getFromAddress()
            out["refs"].append({
                "from": "%x" % frm.getOffset(),
                "type": str(r.getReferenceType()),
                "fromFunction": _func_name_at(frm),
            })
    out["count"] = len(out["refs"])
    # Sentinel-wrapped so callers can extract the JSON from Ghidra's noisy stdout.
    print("@@FOMRE@@" + json.dumps(out) + "@@END@@")


main()
