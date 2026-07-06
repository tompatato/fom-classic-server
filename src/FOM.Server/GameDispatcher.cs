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

    public async Task DispatchAsync(ClientSession peer, ushort opcode, byte[] body, CancellationToken ct)
    {
        switch ((PacketId)opcode)
        {
            case PacketId.LOGIN_REQUEST:
                LoginRequest request = LoginRequest.Parse(body);
                peer.Name = string.IsNullOrEmpty(request.Name) ? $"Player{peer.ConnId}" : request.Name;
                PacketLog.Line($"  login as '{peer.Name}'");
                await peer.SendAsync(BuildLoginReturn(peer), ct);
                break;

            case PacketId.PING:
                if (body.Length >= 4)
                {
                    await peer.SendAsync(new Pong(BinaryPrimitives.ReadUInt32BigEndian(body)), ct);
                }
                break;

            case PacketId.CHAT:
                ChatMessage chat = ChatMessage.Parse(body);
                PacketLog.Line($"  CHAT [{peer.Name}] ch={chat.Channel}: '{chat.Message}'");
                await _host.BroadcastAsync(new ChatBroadcast(chat.SenderId, chat.Channel, peer.Name, chat.Message), ct);
                break;

            // Character loaded, or vort out of the apartment → hand off to the colony.
            case PacketId.LOAD_CHAR:
            case PacketId.EXIT_APT:
                await peer.SendAsync(new EnterWorld(Status: 4, DefaultWorld, Node: 1), ct);
                break;

            default:
                PacketLog.Line($"  ^ UNMAPPED opcode 0x{opcode:X4}");
                break;
        }
    }

    private static LoginReturn BuildLoginReturn(ClientSession peer) => new(
        HeaderId: LoginReturn.DefaultHeaderId,
        Status: 6,
        Stats: new LoginStats(Hp: 100, Stam: 100, Psi: 100, Conc: 100, Uc: 1000, Xp: 100, Bdgt: 0, Pp: 10),
        AppearanceCode: Appearance.Pack(rank: 7, faction: 1, female: false, leg: 1, arm: 1, torso: 1, head: 1, model: 0),
        PlayerId: (uint)(1000 + peer.ConnId),
        World: DefaultWorld,
        AptTier: 1,
        Name: peer.Name,
        Tag: string.Empty,
        Description: string.Empty);
}
