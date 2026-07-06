# find_indexed_calls.py — find indirect CALL/JMP through an indexed vtable, i.e.
# `CALL dword ptr [reg + reg*4]` or `[reg + reg*4 + disp]`. These are message /
# category dispatchers (dynamic method index). Emits sentinel-wrapped JSON.
import json

p = currentProgram


def main():
    fm = p.getFunctionManager()
    listing = p.getListing()
    out = []
    it = listing.getInstructions(True)
    for ins in it:
        mn = ins.getMnemonicString()
        if mn not in ("CALL", "JMP"):
            continue
        txt = str(ins)
        # want a scaled-index memory operand: "[ REG + REG*0x4 ...]"
        if "[" not in txt or "*0x4" not in txt:
            continue
        # exclude static jump tables (target is an address operand) — those have
        # a base that's an absolute address; keep register+register*4 forms
        a = ins.getAddress()
        fn = fm.getFunctionContaining(a)
        out.append({"a": "%x" % a.getOffset(),
                    "insn": txt,
                    "func": str(fn.getName()) if fn else None})
    print("@@FOMRE@@" + json.dumps(out) + "@@END@@")


main()
