# Server test harness

Tools for validating the C# server against the reverse-engineered protocol and
the real client, without hand-eyeballing the game each time.

## Layers

1. **Capture log + analyzer** — the server writes a JSONL of every packet +
   lifecycle event (`FOM_CAPTURE=<file>`), and `FOM.Server analyze <file>`
   summarizes it (traffic histogram, **unmapped opcodes**, errors).
   `just serve CAPTURE=s.jsonl` / `just analyze s.jsonl`.
2. **RE client-binary checks** (`tests/FOM.ClientChecks.Tests`) — assert the
   staged 2006 client still matches our RE facts (no RakNet, WSOCK32, server IP
   table). Run with `FOMC_GAME_DIR` set; skip otherwise.
3. **Headless scenario bot** (`FOM.TestClient.ScenarioClient`) — a scriptable
   stand-in client used by `tests/FOM.Server.Tests/ScenarioTests.cs` to drive
   login/world/chat/ping against the server with no game.
4. **Live-client automation** (`live-client.sh`, this dir) — start the server
   with capture, launch the REAL client via Proton, observe, then analyze.
5. **Golden byte-parity fixtures** — assert the C# builders reproduce the
   reference stub's exact bytes (see `tests/FOM.Server.Tests` golden tests +
   `gen_golden.py`).

## Live end-to-end check

```sh
export FOMC_GAME_DIR=/path/to/fom_openbeta_v1213   # localhost-patched client
just live-check DURATION=90                          # or: tools/harness/live-client.sh
```

Starts the one-service server on `127.0.0.1`, launches the client under Proton,
and — after you log in during the observation window — prints the captured
traffic and any unmapped opcodes. Requires dotnet, Proton Experimental, and a
display. `NO_CLIENT=1` runs the server+analyze plumbing without launching the
game.

Outputs (capture JSONL, server log, client log) land in a temp dir printed at
the end.
