#!/usr/bin/env bash
#
# Live end-to-end harness (layer 4): start the C# server with capture on, launch
# the REAL 2006 client via Proton pointed at it, observe for a while, then stop
# both and analyze the captured session.
#
# Requires: dotnet, a staged client (FOMC_GAME_DIR), Proton Experimental, a
# display, and the localhost patch already applied to the client (so it dials
# 127.0.0.1). Linux/Proton only.
#
# Env knobs:
#   FOMC_GAME_DIR   staged client dir (required)
#   DURATION        seconds to observe (default 60)
#   FOM_BIND        server bind address (default 127.0.0.1)
#   STEAMROOT       Steam root (default ~/.local/share/Steam)
#   OUT             output dir (default a fresh mktemp dir)
#   NO_CLIENT=1     start server + analyze only, don't launch the game (plumbing test)
#
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
GAME="${FOMC_GAME_DIR:?set FOMC_GAME_DIR to the staged client directory}"
DURATION="${DURATION:-60}"
BIND="${FOM_BIND:-127.0.0.1}"
STEAMROOT="${STEAMROOT:-$HOME/.local/share/Steam}"
PROTON="$STEAMROOT/steamapps/common/Proton - Experimental/proton"
OUT="${OUT:-$(mktemp -d)}"
mkdir -p "$OUT"   # created here: a caller-supplied OUT (unlike mktemp -d) may not exist yet
CAP="$OUT/capture.jsonl"

command -v dotnet >/dev/null || { echo "error: dotnet not found" >&2; exit 1; }
[ -f "$GAME/Lithtech.exe" ] || { echo "error: Lithtech.exe not under FOMC_GAME_DIR ($GAME)" >&2; exit 1; }

echo "== building server (Release) =="
dotnet build "$REPO/FOM.slnx" -c Release -clp:ErrorsOnly >/dev/null
SRV="$REPO/src/FOM.Server/bin/Release/net10.0/FOM.Server.dll"

echo "== starting server (bind $BIND, capture -> $CAP) =="
FOM_BIND="$BIND" FOM_CAPTURE="$CAP" setsid dotnet "$SRV" >"$OUT/server.log" 2>&1 </dev/null &
SRVPID=$!
cleanup() {
    pkill -f 'Lithtech.exe' 2>/dev/null || true
    kill -INT "$SRVPID" 2>/dev/null || true   # graceful: flushes the capture
    sleep 1 || true
    kill "$SRVPID" 2>/dev/null || true
}
trap cleanup EXIT
sleep 2  # let the listeners bind

if [ "${NO_CLIENT:-0}" != "1" ]; then
    [ -f "$PROTON" ] || { echo "error: Proton Experimental not found at $PROTON" >&2; exit 1; }
    echo "== launching client via Proton =="
    export STEAM_COMPAT_DATA_PATH="${STEAM_COMPAT_DATA_PATH:-$OUT/protonprefix}"
    export STEAM_COMPAT_CLIENT_INSTALL_PATH="$STEAMROOT"
    export PULSE_SERVER="unix:${XDG_RUNTIME_DIR:-/run/user/$(id -u)}/pulse/native"
    export DISPLAY="${DISPLAY:-:0}"
    mkdir -p "$STEAM_COMPAT_DATA_PATH"
    ( cd "$GAME" && setsid python3 "$PROTON" run Lithtech.exe \
        +windowed 1 -windowtitle "Face of Mankind" -rez Resources -dpsmagic 1 \
        >"$OUT/client.log" 2>&1 </dev/null ) &
    echo "   client starting; log in during the next ${DURATION}s to capture a full session."
fi

echo "== observing ${DURATION}s =="
sleep "$DURATION"

cleanup
trap - EXIT
sleep 1

echo "== capture analysis =="
if [ -s "$CAP" ]; then
    dotnet "$SRV" analyze "$CAP"
    echo "(full capture + server log in $OUT)"
else
    echo "no capture recorded (did the client connect? see $OUT/server.log, $OUT/client.log)"
fi
