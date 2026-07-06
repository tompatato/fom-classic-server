using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace FOM.Server.Tests;

public class SpawnEchoTests
{
    [Fact]
    public async Task SpawnTest_EchoesMovementBackAsCloneEntity()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        CancellationToken ct = timeout.Token;

        await using var server = new TestServer(spawnTest: true);
        using var udp = new UdpClient();
        udp.Connect(IPAddress.Loopback, server.Port);

        // A movement datagram: session 5, X=1000. Heading varies per send so the
        // server's consecutive-duplicate collapse doesn't suppress the echo.
        byte[] dg = new byte[22];
        dg[0] = 0x03;
        dg[1] = 0xF3;
        dg[3] = 0x1C;
        BinaryPrimitives.WriteUInt32BigEndian(dg.AsSpan(4), 5u);
        BinaryPrimitives.WriteUInt16BigEndian(dg.AsSpan(8), 1000);

        for (int i = 0; i < 100; i++)
        {
            ct.ThrowIfCancellationRequested();
            BinaryPrimitives.WriteUInt16BigEndian(dg.AsSpan(0x12), (ushort)i); // vary heading
            await udp.SendAsync(dg, ct);

            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attempt.CancelAfter(TimeSpan.FromMilliseconds(150));
            try
            {
                UdpReceiveResult echo = await udp.ReceiveAsync(attempt.Token);
                // Clone entity id = session + 4242, X offset by +0x100.
                Assert.Equal(5u + 4242, BinaryPrimitives.ReadUInt32BigEndian(echo.Buffer.AsSpan(4)));
                Assert.Equal(1000 + 0x100, BinaryPrimitives.ReadUInt16BigEndian(echo.Buffer.AsSpan(8)));
                return;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // echo not yet received (loss / listener still binding) — retry
            }
        }

        Assert.Fail("no clone-echo movement received");
    }
}
