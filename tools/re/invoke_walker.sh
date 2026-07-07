#!/usr/bin/env bash
# invoke_walker.sh — attach gdb to the live client and DIRECTLY invoke the avatar
# snapshot walker with a hand-built buffer (tools/re/invoke_walker.py), to confirm
# the entry/buffer format independent of transport. See World Object Spawn.md.
#
# Prereqs: ptrace_scope=0, same user as the client, client LOGGED IN + IN-WORLD
# (so Object.lto is mapped). One-shot: attaches, invokes, detaches (game resumes,
# so a spawned avatar persists). Re-run to iterate with different coords.
#
#   tools/re/invoke_walker.sh                      # auto-find in-world pid, pos 0
#   tools/re/invoke_walker.sh <x> <y> <z>          # place at world coords
#   tools/re/invoke_walker.sh <pid> <x> <y> <z>    # explicit pid
#   tools/re/invoke_walker.sh <pid> <x> <y> <z> <appearance>
#
# Tip: use the X/Y/Z from the server's latest "MOVE sess=..." log line so the test
# avatar lands on the player.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# First arg is a pid only if it names a live process; otherwise treat all args as coords.
pid=""
if [[ "${1:-}" =~ ^[0-9]+$ ]] && kill -0 "${1:-}" 2>/dev/null; then
  pid="$1"; shift
fi
if [[ -z "$pid" ]]; then
  for p in $(pgrep -f -i 'Lithtech.exe' 2>/dev/null); do
    if grep -qi 'Object.lto' "/proc/$p/maps" 2>/dev/null; then pid="$p"; break; fi
  done
fi
if [[ -z "$pid" ]]; then
  echo "No in-world Lithtech.exe found (need Object.lto mapped). Enter the world first." >&2
  exit 1
fi
echo "Attaching to in-world client pid=$pid; fom-invoke $*"

exec gdb -p "$pid" -batch \
  -iex 'set pagination off' \
  -iex 'set debuginfod enabled off' \
  -ex "source $here/invoke_walker.py" \
  -ex "fom-invoke $*" \
  -ex 'detach' \
  -ex 'quit'
