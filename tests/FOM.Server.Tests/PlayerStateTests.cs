using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using FOM.Protocol;
using FOM.Server;
using FOM.TestClient;

namespace FOM.Server.Tests;

public class PlayerStateTests
{
    private static CancellationToken Timeout => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    [Fact]
    public async Task Login_RegistersPlayer_AndMovementUpdatesPosition()
    {
        CancellationToken ct = Timeout;
        await using var server = new TestServer();
        WorldId world = WorldPort.FromPort(server.Port);

        await using var client = new ScenarioClient();
        await client.ConnectAsync("127.0.0.1", server.Port, ct);
        await client.SendLoginAsync("Neo", ct);
        byte[] login = await client.ExpectAsync(PacketId.LOGIN_RETURN, ct);

        // Header (+0x00) is the entity/session id the client echoes in movement.
        uint id = BinaryPrimitives.ReadUInt32BigEndian(login);
        Assert.Contains(server.Host.World.InWorld(world), p => p.Name == "Neo" && p.Id == id);

        // Movement (UDP) carrying that session id must update the tracked position.
        byte[] datagram = new byte[22];
        datagram[0] = 0x03;
        datagram[1] = 0xF3;
        datagram[3] = 0x1C;
        BinaryPrimitives.WriteUInt32BigEndian(datagram.AsSpan(4), id);          // session
        BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(0x08), 619);      // X
        BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(0x12), 46170);    // heading

        using var udp = new UdpClient();
        udp.Connect(IPAddress.Loopback, server.Port);
        while (!(server.Host.World.TryGet(id, out Player? p) && p!.LastMovement is not null))
        {
            ct.ThrowIfCancellationRequested();
            await udp.SendAsync(datagram, ct);
            await Task.Delay(50, ct);
        }

        Assert.True(server.Host.World.TryGet(id, out Player? player));
        Assert.Equal(619, player!.LastMovement!.Value.X);
        Assert.Equal(46170, player.LastMovement.Value.Heading);
    }

    [Fact]
    public async Task Disconnect_RemovesPlayerFromWorld()
    {
        CancellationToken ct = Timeout;
        await using var server = new TestServer();
        WorldId world = WorldPort.FromPort(server.Port);

        var client = new ScenarioClient();
        await client.ConnectAsync("127.0.0.1", server.Port, ct);
        await client.SendLoginAsync("Trinity", ct);
        await client.ExpectAsync(PacketId.LOGIN_RETURN, ct);
        Assert.Single(server.Host.World.InWorld(world));

        await client.DisposeAsync(); // drop the connection

        // The disconnect handler removes the player; wait for it to settle.
        while (server.Host.World.InWorld(world).Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(50, ct);
        }
        Assert.Empty(server.Host.World.InWorld(world));
    }
}
