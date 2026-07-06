using System.Net;
using System.Net.Sockets;
using FOM.Server.Capture;

namespace FOM.Server.Tests;

public class UdpCaptureTests
{
    [Fact]
    public async Task Movement_IsCapturedAndRecognized()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        CancellationToken ct = timeout.Token;

        string capturePath = Path.Combine(Path.GetTempPath(), $"fom-udp-{Guid.NewGuid():N}.jsonl");
        var server = new TestServer(capturePath);
        try
        {
            // A framed movement datagram (opcode 0x03F3), same shape as the live client's.
            byte[] datagram =
            [
                0x03, 0xF3, 0x00, 0x1C,
                0x00, 0x00, 0x30, 0x39, // session 12345
                0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x00, 0x00,
                0x00, 0x05, 0x40, 0x00, 0x00, 0x09,
            ];

            // Resend until captured (UDP is lossy and the listener may still be binding);
            // identical resends collapse to one entry via the dedup.
            using var udp = new UdpClient();
            udp.Connect(IPAddress.Loopback, server.Port);
            while (!(File.Exists(capturePath) && File.ReadAllText(capturePath).Contains("0x03F3")))
            {
                ct.ThrowIfCancellationRequested();
                await udp.SendAsync(datagram, ct);
                await Task.Delay(50, ct);
            }

            CaptureReport report = CaptureAnalyzer.AnalyzeFile(capturePath);
            Assert.Contains(report.Traffic, t => t.Key == "C->S 0x03F3 MOVEMENT");
            Assert.DoesNotContain(report.Unmapped, u => u.Key.Contains("0x03F3"));
        }
        finally
        {
            await server.DisposeAsync();
            File.Delete(capturePath);
        }
    }
}
