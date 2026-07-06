using FOM.Protocol;

namespace FOM.Server;

/// <summary>Human-readable labels for opcodes, for logs and capture analysis.</summary>
public static class OpcodeNames
{
    private static readonly Dictionary<ushort, string> Names = new()
    {
        [(ushort)PacketId.LOGIN_REQUEST] = "LOGIN_REQUEST",
        [(ushort)PacketId.LOGIN_RETURN] = "LOGIN_RETURN",
        [(ushort)PacketId.LOAD_CHAR] = "LOAD_CHAR",
        [(ushort)PacketId.PING] = "PING",
        [(ushort)PacketId.ENTER_WORLD] = "ENTER_WORLD",
        [(ushort)PacketId.EXIT_APT] = "EXIT_APT",
        [(ushort)PacketId.ZONE_UPDATE] = "ZONE_UPDATE",
        [(ushort)PacketId.CHAT] = "CHAT",
        [(ushort)PacketId.MOVEMENT] = "MOVEMENT",
        [(ushort)PacketId.POLL] = "POLL",
        [(ushort)PacketId.POST_ENTER] = "POST_ENTER",
        [(ushort)PacketId.WORLD_LOADED] = "WORLD_LOADED",
        [(ushort)PacketId.NODE_REQ] = "NODE_REQ",
        [(ushort)PacketId.KEEPALIVE30_UNCONFIRMED] = "KEEPALIVE30?",
    };

    public static string Get(ushort opcode) => Names.GetValueOrDefault(opcode, "?");
}
