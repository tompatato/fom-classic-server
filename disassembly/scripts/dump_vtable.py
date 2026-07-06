# dump_vtable.py — read a vtable's function pointers and decompile each.
#   analyzeHeadless <proj> FoMClassic -process <program> -noanalysis \
#       -scriptPath disassembly/scripts -postScript dump_vtable.py <vtable-addr> [count]
import sys
from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor

p = currentProgram
args = list(getScriptArgs())
vt = int(args[0], 16) if args else 0x100fa8fc
count = int(args[1]) if len(args) > 1 else 12

af = p.getAddressFactory()
mem = p.getMemory()
fm = p.getFunctionManager()
iface = DecompInterface()
iface.openProgram(p)
mon = ConsoleTaskMonitor()

base = af.getAddress("%x" % vt)
sys.stderr.write("vtable @ %x\n" % vt)
for i in range(count):
    try:
        fnptr = mem.getInt(base.add(i * 4)) & 0xffffffff
    except Exception as e:
        print("@@SLOT@@ %d read-failed %s" % (i, e))
        break
    faddr = af.getAddress("%x" % fnptr)
    fn = fm.getFunctionAt(faddr) or fm.getFunctionContaining(faddr)
    name = fn.getName() if fn else "(no func)"
    print("@@SLOT@@ [%d] vt+0x%x -> %x %s" % (i, i * 4, fnptr, name))
    if fn:
        res = iface.decompileFunction(fn, 60, mon)
        if res and res.decompileCompleted():
            print(res.getDecompiledFunction().getC())
    print("@@ENDSLOT@@")
