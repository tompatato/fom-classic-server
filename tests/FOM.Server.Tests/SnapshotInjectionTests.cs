using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Server.Tests;

public class SnapshotInjectionTests
{
    [Fact]
    public async Task SnapshotTest_PushesOneCharacterSnapshotBesideThePlayer()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        CancellationToken ct = timeout.Token;

        await using var server = new TestServer(snapshotTest: true);

        // Log in over TCP so the player is registered (the snapshot is placed beside a
        // known player, looked up by movement session id). First player => id 1.
        using var tcp = new TcpClient();
        await ConnectWithRetryAsync(tcp, server.Port, ct);
        NetworkStream stream = tcp.GetStream();
        await stream.WriteAsync(new PacketWriter().WriteFixedCString("Neo", 20).ToFrame(PacketId.LOGIN_REQUEST), ct);
        await ReadLoginReturnAsync(stream, ct);
        const uint playerId = 1;

        using var udp = new UdpClient();
        udp.Connect(IPAddress.Loopback, server.Port);

        // Movement for the logged-in player: session = player id, X = 1000. Heading
        // varies per send so the server's duplicate-collapse doesn't drop it.
        byte[] dg = new byte[22];
        dg[0] = 0x03;
        dg[1] = 0xF3;
        dg[3] = 0x1C;
        BinaryPrimitives.WriteUInt32BigEndian(dg.AsSpan(4), playerId);
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
                UdpReceiveResult received = await udp.ReceiveAsync(attempt.Token);
                byte[] snap = received.Buffer;

                // One entry, at the walker's offsets.
                Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(snap.AsSpan(CharacterSnapshot.CountOffset)));
                Span<byte> entry = snap.AsSpan(CharacterSnapshot.EntriesOffset, CharacterSnapshot.EntryStride);
                // Snapshot character id = player id + offset; masked to 24 bits.
                Assert.Equal(playerId + 4243u, BinaryPrimitives.ReadUInt32LittleEndian(entry) & 0x00FFFFFFu);
                // X offset by +0x100 from the movement (low u16 of the packed pos word).
                Assert.Equal(1000u + 0x100u, BinaryPrimitives.ReadUInt32LittleEndian(entry[0x04..]) & 0xFFFFu);
                // Known-good test appearance code.
                Assert.Equal(0x71088820u, BinaryPrimitives.ReadUInt32LittleEndian(entry[0x1C..]));
                return;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // snapshot not yet received (listener still binding / loss) — retry
            }
        }

        Assert.Fail("no character snapshot received");
    }

    private static async Task ReadLoginReturnAsync(NetworkStream stream, CancellationToken ct)
    {
        byte[] header = new byte[PacketFrame.HeaderSize];
        await stream.ReadExactlyAsync(header, ct);
        int bodyLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
        await stream.ReadExactlyAsync(new byte[bodyLength], ct);
    }

    private static async Task ConnectWithRetryAsync(TcpClient client, int port, CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, ct);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(50, ct); // listener not up yet
            }
        }
    }
}
