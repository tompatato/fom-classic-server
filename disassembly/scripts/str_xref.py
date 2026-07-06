# str_xref.py — find defined strings containing any of the given substrings,
# and report the functions that reference each. Emits sentinel-wrapped JSON.
import json

from ghidra.program.model.data import StringDataType

p = currentProgram


def _func_name_at(addr):
    fn = p.getFunctionManager().getFunctionContaining(addr)
    return str(fn.getName()) if fn is not None else None


def main():
    needles = [a.lower() for a in getScriptArgs()]
    rm = p.getReferenceManager()
    listing = p.getListing()
    results = []
    di = listing.getDefinedData(True)
    for d in di:
        dt = d.getDataType()
        if not isinstance(dt, StringDataType):
            continue
        val = d.getValue()
        if val is None:
            continue
        s = str(val)
        low = s.lower()
        if not any(n in low for n in needles):
            continue
        addr = d.getAddress()
        refs = []
        for r in rm.getReferencesTo(addr):
            fa = r.getFromAddress()
            refs.append({"from": "%x" % fa.getOffset(),
                         "func": _func_name_at(fa)})
        results.append({"addr": "%x" % addr.getOffset(),
                        "str": s, "refs": refs})
    print("@@FOMRE@@" + json.dumps(results) + "@@END@@")


main()
