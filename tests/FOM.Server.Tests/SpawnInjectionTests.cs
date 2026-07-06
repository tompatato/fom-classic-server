using FOM.Protocol;
using FOM.Protocol.Messages;
using FOM.TestClient;

namespace FOM.Server.Tests;

public class SpawnInjectionTests
{
    private static CancellationToken Timeout => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    [Fact]
    public async Task SpawnTest_InjectsZoneUpdateAfterWorldEntry()
    {
        CancellationToken ct = Timeout;
        // spawnTest on, tiny delay so the test is fast.
        await using var server = new TestServer(spawnTest: true, spawnDelaySeconds: 0.05);
        await using var client = new ScenarioClient();
        await client.ConnectAsync("127.0.0.1", server.Port, ct);

        await client.SendLoginAsync("Neo", ct);
        await client.ExpectAsync(PacketId.LOGIN_RETURN, ct);
        await client.SendLoadCharAsync(ct); // triggers ENTER_WORLD, then the injected spawn

        // Skip ENTER_WORLD; the injected 0x082D spawn should arrive.
        byte[] spawn = await client.ExpectAsync(PacketId.ZONE_UPDATE, ct);
        Assert.Equal(SpawnZoneUpdate.BodyLength, spawn.Length);
    }

    [Fact]
    public async Task SpawnTest_Off_SendsNoZoneUpdate()
    {
        CancellationToken ct = Timeout;
        await using var server = new TestServer(); // spawnTest defaults off
        await using var client = new ScenarioClient();
        await client.ConnectAsync("127.0.0.1", server.Port, ct);

        await client.SendLoginAsync("Neo", ct);
        await client.ExpectAsync(PacketId.LOGIN_RETURN, ct);
        await client.SendLoadCharAsync(ct);

        // We should get ENTER_WORLD and nothing else; assert no ZONE_UPDATE within a window.
        (PacketId opcode, _) = await client.ReadFrameAsync(ct);
        Assert.Equal(PacketId.ENTER_WORLD, opcode);

        using var quiet = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await client.ReadFrameAsync(quiet.Token));
    }
}
