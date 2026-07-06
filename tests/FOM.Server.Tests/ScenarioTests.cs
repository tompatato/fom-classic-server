using System.Buffers.Binary;
using FOM.Protocol;
using FOM.Protocol.Messages;
using FOM.TestClient;

namespace FOM.Server.Tests;

public class ScenarioTests
{
    private static CancellationToken Timeout => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    [Fact]
    public async Task LoginThenLoadChar_WalksIntoTheWorld()
    {
        CancellationToken ct = Timeout;
        await using var server = new TestServer();
        await using var client = new ScenarioClient();
        await client.ConnectAsync("127.0.0.1", server.Port, ct);

        await client.SendLoginAsync("Neo", ct);
        byte[] login = await client.ExpectAsync(PacketId.LOGIN_RETURN, ct);
        Assert.Equal(620, login.Length);

        await client.SendLoadCharAsync(ct);
        byte[] enter = await client.ExpectAsync(PacketId.ENTER_WORLD, ct);
        // status(4) | world(the one the client connected on) | node(1) | pad
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32BigEndian(enter));
        Assert.Equal((uint)WorldPort.FromPort(server.Port), BinaryPrimitives.ReadUInt32BigEndian(enter.AsSpan(4)));
    }

    [Fact]
    public async Task Ping_IsPongedWithSameTimestamp()
    {
        CancellationToken ct = Timeout;
        await using var server = new TestServer();
        await using var client = new ScenarioClient();
        await client.ConnectAsync("127.0.0.1", server.Port, ct);

        await client.SendPingAsync(0xCAFEBABE, ct);
        byte[] pong = await client.ExpectAsync(PacketId.PING, ct);
        Assert.Equal(0xCAFEBABEu, BinaryPrimitives.ReadUInt32BigEndian(pong));
    }

    [Fact]
    public async Task Chat_IsBroadcastToOtherClients()
    {
        CancellationToken ct = Timeout;
        await using var server = new TestServer();

        await using var alice = new ScenarioClient();
        await using var bob = new ScenarioClient();
        await alice.ConnectAsync("127.0.0.1", server.Port, ct);
        await bob.ConnectAsync("127.0.0.1", server.Port, ct);

        // Both log in so the server knows their display names.
        await alice.SendLoginAsync("Alice", ct);
        await alice.ExpectAsync(PacketId.LOGIN_RETURN, ct);
        await bob.SendLoginAsync("Bob", ct);
        await bob.ExpectAsync(PacketId.LOGIN_RETURN, ct);

        await alice.SendChatAsync(senderId: 1, channel: 0, name: "Alice", message: "hello world", ct);

        // Bob receives the broadcast, attributed to Alice's session name.
        byte[] body = await bob.ExpectAsync(PacketId.CHAT, ct);
        ChatMessage chat = ChatMessage.Parse(body);
        Assert.Equal("Alice", chat.Name);
        Assert.Equal("hello world", chat.Message);
    }
}
