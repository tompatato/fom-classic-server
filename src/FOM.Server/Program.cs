using FOM.Protocol;
using FOM.Server;
using FOM.Server.Capture;

// Analysis mode: `FOM.Server analyze <capture.jsonl>` prints a session summary.
if (args is ["analyze", var capturePathArg, ..])
{
    Console.WriteLine(CaptureAnalyzer.AnalyzeFile(capturePathArg).Render());
    return;
}

// One service for all worlds. Defaults to loopback (the localhost-patched client);
// override the bind address / port range / capture path via env vars.
string bind = Environment.GetEnvironmentVariable("FOM_BIND") ?? "127.0.0.1";
int firstPort = int.TryParse(Environment.GetEnvironmentVariable("FOM_FIRST_PORT"), out int f) ? f : WorldPort.FirstPort;
int lastPort = int.TryParse(Environment.GetEnvironmentVariable("FOM_LAST_PORT"), out int l) ? l : WorldPort.FirstPort + 45;
string? capture = Environment.GetEnvironmentVariable("FOM_CAPTURE");
bool spawnTest = Environment.GetEnvironmentVariable("FOM_SPAWN_TEST") == "1";
bool snapshotTest = Environment.GetEnvironmentVariable("FOM_SNAPSHOT_TEST") == "1";
bool snapshotRepeat = Environment.GetEnvironmentVariable("FOM_SNAPSHOT_REPEAT") == "1";
double spawnDelay = double.TryParse(Environment.GetEnvironmentVariable("FOM_SPAWN_DELAY"), out double d) ? d : 6;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("shutting down...");
    cts.Cancel();
};

var host = new GameHost(bind, firstPort, lastPort, capture, spawnTest, spawnDelay,
    snapshotTest: snapshotTest, snapshotRepeat: snapshotRepeat);
await host.RunAsync(cts.Token);
