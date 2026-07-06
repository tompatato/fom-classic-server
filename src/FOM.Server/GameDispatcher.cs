using System.Buffers.Binary;
using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Server;

/// <summary>
/// Routes a decoded client packet to a response, mirroring the reference stub's
/// <c>dispatch()</c>. Stub-level behavior: enough canned responses to walk the
/// client through login → character load → world entry, plus chat relay.
/// </summary>
public sealed class GameDispatcher(GameHost host)
{
    /// <summary>The colony the stub drops every player into.</summary>
    public const WorldId DefaultWorld = WorldId.StsGenesis;

    private readonly GameHost _host = host;

    /// <summary>Handles the packet and returns whether an opcode handler existed.</summary>
    public async Task<bool> DispatchAsync(ClientSession peer, ushort opcode, byte[] body, CancellationToken ct)
    {
        switch ((PacketId)opcode)
        {
            case PacketId.LOGIN_REQUEST:
                LoginRequest request = LoginRequest.Parse(body);
                peer.Name = string.IsNullOrEmpty(request.Name) ? $"Player{peer.ConnId}" : request.Name;
                Player player = _host.RegisterPlayer(peer, peer.Name);
                PacketLog.Line($"  login as '{peer.Name}' (player {player.Id}, {player.World})");
                await peer.SendAsync(BuildLoginReturn(player), ct);
                return true;

            case PacketId.PING:
                if (body.Length >= 4)
                {
                    await peer.SendAsync(new Pong(BinaryPrimitives.ReadUInt32BigEndian(body)), ct);
                }
                return true;

            case PacketId.CHAT:
                ChatMessage chat = ChatMessage.Parse(body);
                PacketLog.Line($"  CHAT [{peer.Name}] ch={chat.Channel}: '{chat.Message}'");
                await _host.BroadcastAsync(new ChatBroadcast(chat.SenderId, chat.Channel, peer.Name, chat.Message), ct);
                return true;

            // Character loaded, or vort out of the apartment → hand off to the colony.
            case PacketId.LOAD_CHAR:
            case PacketId.EXIT_APT:
                await peer.SendAsync(new EnterWorld(Status: 4, DefaultWorld, Node: 1), ct);
                if (_host.SpawnTest)
                {
                    ScheduleSpawnInjection(peer, _host.SpawnDelay, ct);
                }
                return true;

            default:
                PacketLog.Line($"  ^ UNMAPPED opcode 0x{opcode:X4}");
                return false;
        }
    }

    // Experiment (3c): after the client is in the world, inject a 0x082D zone update
    // for a clone entity (id distinct from the player's) and see whether an avatar
    // appears. Fire-and-forget; failures are logged, never fatal.
    private static void ScheduleSpawnInjection(ClientSession peer, TimeSpan delay, CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, ct);
                uint cloneId = (peer.Player?.Id ?? 0) + 4242; // distinct from the player's own id
                PacketLog.Line($"  [spawn-test] injecting 0x082D clone entity {cloneId}");
                await peer.SendAsync(new SpawnZoneUpdate(cloneId, "CLONE", SpawnZoneUpdate.DefaultAppearance), ct);
            }
            catch (OperationCanceledException)
            {
                // server shutting down
            }
            catch (Exception e)
            {
                PacketLog.Line($"  [spawn-test] injection failed: {e.Message}");
            }
        }, ct);
    }

    private static LoginReturn BuildLoginReturn(Player player) => new(
        HeaderId: player.Id,   // entity/session id — echoed by the client in movement
        Status: 6,
        Stats: new LoginStats(Hp: 100, Stam: 100, Psi: 100, Conc: 100, Uc: 1000, Xp: 100, Bdgt: 0, Pp: 10),
        AppearanceCode: Appearance.Pack(rank: 7, faction: 1, female: false, leg: 1, arm: 1, torso: 1, head: 1, model: 0),
        PlayerId: player.Id,
        World: player.World,
        AptTier: 1,
        Name: player.Name,
        Tag: string.Empty,
        Description: string.Empty);
}
