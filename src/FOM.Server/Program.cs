using FOM.Protocol;
using FOM.Server;

// One service for all worlds. Defaults to loopback (the localhost-patched client);
// override the bind address and port range via env vars for a remote deployment.
string bind = Environment.GetEnvironmentVariable("FOM_BIND") ?? "127.0.0.1";
int firstPort = int.TryParse(Environment.GetEnvironmentVariable("FOM_FIRST_PORT"), out int f) ? f : WorldPort.FirstPort;
int lastPort = int.TryParse(Environment.GetEnvironmentVariable("FOM_LAST_PORT"), out int l) ? l : WorldPort.FirstPort + 45;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("shutting down...");
    cts.Cancel();
};

var host = new GameHost(bind, firstPort, lastPort);
await host.RunAsync(cts.Token);
