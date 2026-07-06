# find_vcalls.py — scan the whole program for indirect CALLs through a vtable at
# a given set of displacements, i.e. `CALL dword ptr [reg + disp]`. Reports the
# containing function and the instruction. Args: one or more hex displacements
# (e.g. 0x48 0x4c). Emits sentinel-wrapped JSON.
import json

from ghidra.program.model.lang import OperandType

p = currentProgram


def main():
    wanted = set()
    for a in getScriptArgs():
        wanted.add(int(a, 16))
    fm = p.getFunctionManager()
    listing = p.getListing()
    out = []
    it = listing.getInstructions(True)
    for ins in it:
        mn = ins.getMnemonicString()
        if mn not in ("CALL", "JMP"):
            continue
        # operand 0 is the target; look for dynamic [reg + disp]
        if ins.getNumOperands() < 1:
            continue
        ot = ins.getOperandType(0)
        if not (OperandType.isDynamic(ot) and OperandType.isAddress(ot) is False):
            # we want register+displacement indirection; check the scalar
            pass
        # extract displacement scalar if present
        scalar = None
        try:
            objs = ins.getOpObjects(0)
            for o in objs:
                cn = o.getClass().getSimpleName()
                if cn == "Scalar":
                    scalar = o.getValue() & 0xffffffff
        except Exception:
            pass
        if scalar is None or scalar not in wanted:
            continue
        # must be an indirect (register-based) call, not a direct/absolute
        txt = str(ins)
        if "[" not in txt:
            continue
        a = ins.getAddress()
        fn = fm.getFunctionContaining(a)
        out.append({"a": "%x" % a.getOffset(),
                    "disp": "%x" % scalar,
                    "insn": txt,
                    "func": str(fn.getName()) if fn else None})
    print("@@FOMRE@@" + json.dumps(out) + "@@END@@")


main()
