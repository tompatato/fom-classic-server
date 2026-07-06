using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using FOM.Protocol;
using FOM.Protocol.Messages;
using FOM.Server;

namespace FOM.Server.Tests;

public class LoginIntegrationTests
{
    [Fact]
    public async Task ClientLogin_ReceivesWellFormedLoginReturn()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        CancellationToken ct = timeout.Token;

        int port = GetFreePort();
        using var serverCts = new CancellationTokenSource();
        var host = new GameHost("127.0.0.1", port, port);
        Task runTask = host.RunAsync(serverCts.Token);

        using var client = new TcpClient();
        await ConnectWithRetryAsync(client, port, ct);
        NetworkStream stream = client.GetStream();

        // LOGIN_REQUEST carrying name "Neo" in the 20-byte field.
        byte[] request = new PacketWriter().WriteFixedCString("Neo", 20).ToFrame(PacketId.LOGIN_REQUEST);
        await stream.WriteAsync(request, ct);

        // Expect LOGIN_RETURN back: 4-byte header, then a 620-byte body.
        byte[] header = new byte[PacketFrame.HeaderSize];
        await stream.ReadExactlyAsync(header, ct);
        Assert.Equal((ushort)PacketId.LOGIN_RETURN, BinaryPrimitives.ReadUInt16BigEndian(header));
        int bodyLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
        Assert.Equal(620, bodyLength);

        byte[] body = new byte[bodyLength];
        await stream.ReadExactlyAsync(body, ct);

        // Player id echoed at +0x24 (1000 + connId; first connection => 1001).
        Assert.Equal(1001u, BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(0x24)));
        // Name round-trips at +0x5A.
        var reader = new PacketReader(body.AsSpan(0x5A));
        Assert.Equal("Neo", reader.ReadFixedCString(LoginReturn.NameLength));

        serverCts.Cancel();
        client.Close();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
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
