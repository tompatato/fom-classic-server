# invoke_walker.py — gdb Python hook that DIRECTLY invokes the FoM 2006 avatar
# snapshot walker with a hand-built buffer, to decouple the two open unknowns:
#   (a) the entry/buffer FORMAT   vs   (b) the TRANSPORT that makes the engine call it.
#
# ⚠️ NEGATIVE RESULT (2026-07-07): THIS DOES NOT WORK — it CRASHES the client.
# Calling the walker out of the engine's normal server-update tick returns 0 in gdb
# but leaves the game wedged; the client freezes and dies (SIGSEGV on forced kill).
# Root cause: FUN_10035930's CreateObject -> appearance/model resolution -> object
# registration is not safe to run outside the engine tick. Kept as a documented
# dead-end. Do NOT run against a client you care about. See World Object Spawn.md.
#
# Background (knowledge-base/client/World Object Spawn.md): a remote avatar is only
# ever created by the walker FUN_10036fc0 -> FUN_10035930 (the sole CCharacter
# creator). The walker is engine-invoked (vtable[19]); our app-UDP snapshot never
# reaches it. This script sidesteps transport: it builds the snapshot buffer the
# walker reads (count@+0x14, one 32-byte entry@+0x18) in the inferior and calls the
# walker itself. If an avatar renders, the FORMAT is confirmed and only transport
# remains; if not, the entry fields are wrong.
#
# Usage (ptrace_scope=0, same user, client IN-WORLD so Object.lto is loaded):
#   gdb -p <pid> -x tools/re/invoke_walker.py
#   (gdb) fom-invoke                      # defaults: id 4244, appearance 0x71088820, pos 0
#   (gdb) fom-invoke <x> <y> <z>          # place at world coords (use the server's last MOVE log)
#   (gdb) fom-invoke <x> <y> <z> <appear> # override appearance code too
import re

GHIDRA_IMAGE_BASE = 0x10000000
RVA_WALKER  = 0x36fc0   # FUN_10036fc0(this=session, snapshot)
RVA_SPAWN   = 0x35930   # FUN_10035930  CreateObject CCharacter
RVA_SESSION = 0xb42f0   # DAT_100b42f0  live session / game object (walker's `this`)
RVA_OBJMGR  = 0xb42e0   # DAT_100b42e0  object manager (id lookups)
MODULE_NEEDLE = "object.lto"

TEST_ID     = 4244        # low-24 id; type nibble 0 => character
TEST_APPEAR = 0x71088820  # known-good live appearance (male, race nibble 1)


def _pid():
    return gdb.selected_inferior().pid


def _module_base(needle):
    lo = None
    with open("/proc/%d/maps" % _pid()) as f:
        for line in f:
            m = re.match(r"([0-9a-f]+)-([0-9a-f]+)\s+\S+\s+\S+\s+\S+\s+\S+\s*(.*)",
                         line.strip())
            if m and needle in m.group(3).lower():
                a = int(m.group(1), 16)
                lo = a if lo is None else min(lo, a)
    return lo


def _u32(addr):
    return int(gdb.parse_and_eval("*(unsigned int *)0x%x" % addr)) & 0xffffffff


def _set8(addr, val):
    gdb.execute("set *(unsigned char *)0x%x = %d" % (addr, val & 0xff), to_string=True)


def _set16(addr, val):
    gdb.execute("set *(unsigned short *)0x%x = %d" % (addr, val & 0xffff), to_string=True)


def _set32(addr, val):
    gdb.execute("set *(unsigned int *)0x%x = %u" % (addr, val & 0xffffffff), to_string=True)


class FomInvoke(gdb.Command):
    def __init__(self):
        super().__init__("fom-invoke", gdb.COMMAND_USER)

    def invoke(self, arg, from_tty):
        # Wine/Proton uses these signals internally; gdb must pass them through or
        # the inferior call freezes/kills the client (same as the spawn hook).
        for sig in ("SIGSEGV", "SIGUSR1", "SIGUSR2", "SIGPIPE", "SIGQUIT",
                    "SIG32", "SIG33", "SIG34", "SIG35", "SIG36", "SIG37", "SIG38"):
            try:
                gdb.execute("handle %s nostop noprint pass" % sig, to_string=True)
            except gdb.error:
                pass
        # If the inferior call faults, unwind back to the pre-call state instead of
        # leaving the client's thread stranded in the dummy frame (which corrupts it).
        gdb.execute("set unwindonsignal on", to_string=True)
        args = arg.split()
        x = int(args[0]) if len(args) > 0 else 0
        y = int(args[1]) if len(args) > 1 else 0
        z = int(args[2]) if len(args) > 2 else 0
        appear = int(args[3], 0) if len(args) > 3 else TEST_APPEAR

        base = _module_base(MODULE_NEEDLE)
        if base is None:
            print("Object.lto not mapped — enter the world first.")
            return
        walker = base + RVA_WALKER
        session = _u32(base + RVA_SESSION)
        objmgr = _u32(base + RVA_OBJMGR)
        print("Object.lto @0x%x  walker=0x%x  session=0x%x  objmgr=0x%x"
              % (base, walker, session, objmgr))
        if session == 0:
            print("session ptr is NULL — not in world yet.")
            return

        # Allocate a snapshot buffer in the inferior (libc malloc is mapped under Wine).
        buf = int(gdb.parse_and_eval("(unsigned int)malloc(0x40)")) & 0xffffffff
        if buf == 0:
            print("malloc failed")
            return
        for off in range(0, 0x40, 4):
            _set32(buf + off, 0)
        # Header the walker reads.
        _set16(buf + 0x14, 1)                       # count = 1
        # One 32-byte entry @ +0x18 (native little-endian, as the walker reads raw).
        e = buf + 0x18
        _set32(e + 0x00, TEST_ID & 0x00ffffff)      # id (type nibble 0 => character)
        _set32(e + 0x04, (x & 0xffff) | ((z & 0xffff) << 16))  # pos x | z<<16
        _set32(e + 0x08, y & 0xffff)                # pos y
        _set32(e + 0x0c, 0)                         # flags (node 0, spawn, no skip)
        _set32(e + 0x1c, appear)                    # appearance

        print("invoking walker(session=0x%x, buf=0x%x)  id=%d pos=(%d,%d,%d) appear=0x%x"
              % (session, buf, TEST_ID, x, y, z, appear))
        # thiscall: `this` in ECX, snapshot on the stack.
        gdb.execute("set $ecx = 0x%x" % session, to_string=True)
        try:
            ret = gdb.parse_and_eval("((int(*)(int))0x%x)(0x%x)" % (walker, buf))
            print("walker returned %s -- check the client for a new avatar at your position." % ret)
        except gdb.error as ex:
            print("call failed: %s" % ex)


FomInvoke()
print("loaded FoM walker-invoke. enter world, then run:  fom-invoke [x y z [appearance]]")
