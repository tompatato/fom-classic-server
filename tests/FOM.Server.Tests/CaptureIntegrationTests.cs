using System.Net;
using System.Net.Sockets;
using FOM.Protocol;
using FOM.Server.Capture;

namespace FOM.Server.Tests;

public class CaptureIntegrationTests
{
    [Fact]
    public async Task Session_IsCaptured_AndUnmappedOpcodeIsDetected()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        CancellationToken ct = timeout.Token;

        string capturePath = Path.Combine(Path.GetTempPath(), $"fom-capture-{Guid.NewGuid():N}.jsonl");
        int port = GetFreePort();
        using var serverCts = new CancellationTokenSource();
        var host = new GameHost("127.0.0.1", port, port, capturePath);
        Task runTask = host.RunAsync(serverCts.Token);

        try
        {
            using var client = new TcpClient();
            await ConnectWithRetryAsync(client, port, ct);
            NetworkStream stream = client.GetStream();

            // A handled packet (login) ...
            await stream.WriteAsync(new PacketWriter().WriteFixedCString("Neo", 20).ToFrame(PacketId.LOGIN_REQUEST), ct);
            byte[] header = new byte[PacketFrame.HeaderSize];
            await stream.ReadExactlyAsync(header, ct); // wait for LOGIN_RETURN so we know it was processed

            // ... and an unmapped one (POLL keepalive, no handler).
            await stream.WriteAsync(PacketFrame.Encode(PacketId.POLL, [0x00, 0x00]), ct);

            // Give the server a moment to record the second packet, then stop it.
            await WaitForAsync(() => File.Exists(capturePath) && File.ReadAllText(capturePath).Contains("0x0822"), ct);

            serverCts.Cancel();
            client.Close();
            try { await runTask; } catch (OperationCanceledException) { }

            CaptureReport report = CaptureAnalyzer.AnalyzeFile(capturePath);
            Assert.Equal(1, report.Connects);
            Assert.Contains(report.Traffic, t => t.Key == "C->S 0x07D1 LOGIN_REQUEST");
            Assert.Contains(report.Traffic, t => t.Key == "S->C 0x07D2 LOGIN_RETURN");
            Assert.Contains(report.Unmapped, u => u.Key == "0x0822 POLL");
        }
        finally
        {
            serverCts.Cancel();
            try { await runTask; } catch (OperationCanceledException) { }
            File.Delete(capturePath);
        }
    }

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task ConnectWithRetryAsync(TcpClient client, int port, CancellationToken ct)
    {
        while (true)
        {
            try { await client.ConnectAsync(IPAddress.Loopback, port, ct); return; }
            catch (SocketException) { await Task.Delay(50, ct); }
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(25, ct);
        }
    }
}
