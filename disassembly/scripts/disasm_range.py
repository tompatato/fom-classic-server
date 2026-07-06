# disasm_range.py — dump raw disassembly for [start,end) plus any function that
# Ghidra thinks contains start. Also tries to CreateFunction at start if none.
# Args: <start-hex> <end-hex> [makefunc]
import json

from ghidra.app.cmd.function import CreateFunctionCmd
from ghidra.program.model.symbol import RefType

p = currentProgram


def main():
    args = list(getScriptArgs())
    af = p.getAddressFactory()

    def A(h):
        return af.getAddress(h[2:] if h.lower().startswith("0x") else h)

    start = A(args[0])
    end = A(args[1]) if len(args) > 1 else start.add(0x120)
    make = len(args) > 2 and args[2] == "makefunc"

    fm = p.getFunctionManager()
    out = {"start": "%x" % start.getOffset(),
           "containing": None, "lines": [], "made": None}
    fn = fm.getFunctionContaining(start)
    if fn is None and make:
        cmd = CreateFunctionCmd(start)
        cmd.applyTo(p)
        fn = fm.getFunctionContaining(start)
        out["made"] = "%x" % fn.getEntryPoint().getOffset() if fn else "failed"
    if fn is not None:
        out["containing"] = str(fn.getName())

    listing = p.getListing()
    rm = p.getReferenceManager()
    ci = listing.getCodeUnits(start, True)
    for cu in ci:
        a = cu.getAddress()
        if a.getOffset() >= end.getOffset():
            break
        refs = []
        for r in rm.getReferencesFrom(a):
            to = r.getToAddress()
            s = p.getSymbolTable().getPrimarySymbol(to)
            refs.append(("%x" % to.getOffset(), str(s.getName()) if s else None))
        out["lines"].append({"a": "%x" % a.getOffset(),
                             "t": str(cu.toString()),
                             "refs": refs})
    print("@@FOMRE@@" + json.dumps(out) + "@@END@@")


main()
