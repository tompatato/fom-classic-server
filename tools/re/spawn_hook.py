# spawn_hook.py — gdb Python hook to capture the FoM 2006 avatar-spawn at runtime.
#
# Attaches to the live client (Lithtech.exe under Proton/Wine), breaks on the two
# Object.lto spawn functions, and dumps: the entity-snapshot buffer that drives the
# spawn, and the calling module (which reveals the transport — engine UDP vs. TCP).
#
# Usage (ptrace_scope must be 0, same user):
#   PID=$(pgrep -f Lithtech.exe | head -1)
#   gdb -p "$PID" -x tools/re/spawn_hook.py
# then in gdb:  fom-hook        (arms the breakpoints and continues)
#
# What it reads (from World Object Spawn.md):
#   FUN_10036fc0(this, snapshot)  RVA 0x36fc0  — snapshot walker (spawns new chars)
#     snapshot+0x14 = u16 count ; snapshot+0x18 = count x 0x20-byte entries
#     entry: id/type@0x00, packed pos@0x04(x|z)/0x08(y), flags@0x0C, appearance@0x1C
#   FUN_10035930(this, entry, &appearance)  RVA 0x35930 — creates one CCharacter
#   At each entry [esp]=return addr (the real caller — thunk JMPs in), [esp+4..]=args.
import re

GHIDRA_IMAGE_BASE = 0x10000000
RVA_WALKER = 0x36fc0   # FUN_10036fc0
RVA_SPAWN  = 0x35930   # FUN_10035930
MODULE_NEEDLE = "object.lto"


def _pid():
    return gdb.selected_inferior().pid


def _maps():
    rows = []
    with open("/proc/%d/maps" % _pid()) as f:
        for line in f:
            m = re.match(r"([0-9a-f]+)-([0-9a-f]+)\s+(\S+)\s+(\S+)\s+\S+\s+\S+\s*(.*)",
                         line.strip())
            if not m:
                continue
            rows.append((int(m.group(1), 16), int(m.group(2), 16),
                         m.group(3), m.group(5).strip()))
    return rows


def _module_base(needle):
    lo = None
    for a, b, perm, path in _maps():
        if needle in path.lower():
            lo = a if lo is None else min(lo, a)
    return lo


def _module_of(addr):
    for a, b, perm, path in _maps():
        if a <= addr < b and path:
            return "%s @+0x%x" % (path.split("/")[-1], addr - a)
    return "0x%x (anon)" % addr


def _u32(addr):
    return int(gdb.parse_and_eval("*(unsigned int *)0x%x" % addr)) & 0xffffffff


def _u16(addr):
    return int(gdb.parse_and_eval("*(unsigned short *)0x%x" % addr)) & 0xffff


def _dump_snapshot(buf):
    if buf == 0:
        print("  snapshot=NULL")
        return
    count = _u16(buf + 0x14)
    print("  snapshot @0x%x  count=%d" % (buf, count))
    for i in range(min(count, 50)):
        e = buf + 0x18 + i * 0x20
        w0 = _u32(e + 0x00)
        pos = _u32(e + 0x04)
        y = _u32(e + 0x08)
        flags = _u32(e + 0x0c)
        appear = _u32(e + 0x1c)
        x = pos & 0xffff
        z = (pos >> 16) & 0xffff
        print("    [%2d] id=%d type=%d  x=%d z=%d y=%d  flags=0x%x  appear=0x%x"
              % (i, w0 & 0xffffff, (w0 >> 24) & 0xf, x, z, y & 0xffff, flags, appear))


def _stack_callers(n=6):
    sp = int(gdb.parse_and_eval("$sp"))
    out = []
    for i in range(n):
        try:
            v = _u32(sp + i * 4)
        except gdb.error:
            break
        out.append("[esp+0x%x]=%s" % (i * 4, _module_of(v)))
    return out


class _Walker(gdb.Breakpoint):
    hits = 0
    CAP = 5  # walker may fire often; log a few then disarm so the game stays live

    def stop(self):
        sp = int(gdb.parse_and_eval("$sp"))
        ret = _u32(sp)
        buf = _u32(sp + 4)
        print("\n=== FUN_10036fc0 (snapshot walker) hit #%d ===" % (_Walker.hits + 1))
        print("  called from: %s" % _module_of(ret))
        _dump_snapshot(buf)
        for c in _stack_callers():
            print("  " + c)
        _Walker.hits += 1
        if _Walker.hits >= _Walker.CAP:
            self.enabled = False
            print("  (walker disarmed after %d hits; spawn breakpoint stays)" % _Walker.CAP)
        return False  # log and auto-continue


class _Spawn(gdb.Breakpoint):
    def stop(self):
        sp = int(gdb.parse_and_eval("$sp"))
        ret = _u32(sp)
        entry = _u32(sp + 4)
        appear_ptr = _u32(sp + 8)
        appear = _u32(appear_ptr) if appear_ptr else 0
        w0 = _u32(entry) if entry else 0
        print("\n=== FUN_10035930 (CreateObject CCharacter) ===")
        print("  called from: %s" % _module_of(ret))
        print("  entry@0x%x id=%d type=%d appearance=0x%x"
              % (entry, w0 & 0xffffff, (w0 >> 24) & 0xf, appear))
        return False


class FomHook(gdb.Command):
    def __init__(self):
        super().__init__("fom-hook", gdb.COMMAND_USER)

    def invoke(self, arg, from_tty):
        base = _module_base(MODULE_NEEDLE)
        if base is None:
            print("Object.lto not mapped yet — enter the world first, then re-run fom-hook")
            return
        print("Object.lto base = 0x%x" % base)
        _Walker("*0x%x" % (base + RVA_WALKER))
        _Spawn("*0x%x" % (base + RVA_SPAWN))
        print("armed: walker@0x%x spawn@0x%x — continuing"
              % (base + RVA_WALKER, base + RVA_SPAWN))
        gdb.execute("continue")


FomHook()
print("loaded FoM spawn hook. run:  fom-hook")
