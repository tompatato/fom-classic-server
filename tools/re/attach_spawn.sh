#!/usr/bin/env bash
# attach_spawn.sh — attach gdb to the live FoM 2006 client and arm the avatar-spawn
# hook (tools/re/spawn_hook.py). Captures the entity-snapshot buffer and the calling
# module at each CCharacter spawn, to pin the spawn transport + wire format.
#
# Prereqs: ptrace_scope=0 (cat /proc/sys/kernel/yama/ptrace_scope), same user as the
# client. LOG IN AND ENTER THE WORLD FIRST so Object.lto is loaded, then run this.
#
#   tools/re/attach_spawn.sh                 # auto-find Lithtech.exe
#   tools/re/attach_spawn.sh <pid>           # explicit pid
#
# In gdb: it arms the breakpoints and continues automatically. Move around / watch
# for other players. Ctrl-C then `quit` to detach (the game resumes on detach).
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

pid="${1:-}"
if [[ -z "$pid" ]]; then
  pid="$(pgrep -f -i 'Lithtech.exe' | head -1 || true)"
fi
if [[ -z "$pid" ]]; then
  echo "No Lithtech.exe process found. Launch the client and enter the world first." >&2
  exit 1
fi
echo "Attaching to Lithtech.exe pid=$pid"

exec gdb -p "$pid" \
  -iex 'set pagination off' \
  -iex 'set debuginfod enabled off' \
  -ex "source $here/spawn_hook.py" \
  -ex 'fom-hook'
