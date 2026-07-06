# dump_ptrs.py — dump a range as an array of 4-byte little-endian pointers,
# annotating each with the symbol/function at the target. Args: <start> <count>
import json

p = currentProgram


def main():
    args = list(getScriptArgs())
    af = p.getAddressFactory()
    mem = p.getMemory()
    st = p.getSymbolTable()
    fm = p.getFunctionManager()
    start = af.getAddress(args[0][2:] if args[0].lower().startswith("0x") else args[0])
    n = int(args[1]) if len(args) > 1 else 64
    out = []
    for i in range(n):
        a = start.add(i * 4)
        try:
            v = mem.getInt(a) & 0xffffffff
        except Exception:
            out.append({"slot": i, "at": "%x" % a.getOffset(), "err": "unreadable"})
            continue
        target = af.getAddress("%x" % v)
        sym = st.getPrimarySymbol(target) if target is not None else None
        fn = fm.getFunctionContaining(target) if target is not None else None
        out.append({"slot": i, "at": "%x" % a.getOffset(), "val": "%x" % v,
                    "sym": str(sym.getName()) if sym else None,
                    "func": str(fn.getName()) if fn else None})
    print("@@FOMRE@@" + json.dumps(out) + "@@END@@")


main()
