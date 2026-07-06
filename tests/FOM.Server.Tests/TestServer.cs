using System.Net;
using System.Net.Sockets;

namespace FOM.Server.Tests;

/// <summary>Starts a <see cref="GameHost"/> on a free loopback port for a test, and stops it on dispose.</summary>
public sealed class TestServer : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _run;

    public int Port { get; }

    /// <summary>The running host — exposes <see cref="GameHost.World"/> for assertions.</summary>
    public GameHost Host { get; }

    public TestServer(string? capturePath = null)
    {
        Port = FreePort();
        Host = new GameHost("127.0.0.1", Port, Port, capturePath);
        _run = Host.RunAsync(_cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _run;
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        _cts.Dispose();
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
