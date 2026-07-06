using System.Text;

namespace FOM.Protocol.Messages;

/// <summary>
/// <see cref="PacketId.LOGIN_RETURN"/> — the server's login response. Fixed 620-byte
/// body; field offsets and the little-endian <c>Pp</c> quirk are reproduced from the
/// reference stub's <c>send_login</c>.
/// </summary>
public sealed record LoginReturn(
    uint HeaderId,
    ushort Status,
    LoginStats Stats,
    uint AppearanceCode,
    uint PlayerId,
    WorldId World,
    byte AptTier,
    string Name,
    string Tag,
    string Description) : IServerMessage
{
    /// <summary>The constant the stub writes at body offset 0x00.</summary>
    public const uint DefaultHeaderId = 12345;

    public const int NameLength = 20;
    public const int TagLength = 4;
    public const int DescriptionLength = 506;

    public PacketId Id => PacketId.LOGIN_RETURN;

    public void WriteBody(PacketWriter w)
    {
        w.WriteU32(HeaderId);          // +0x00
        w.WriteU16(Status);            // +0x04
        w.WriteU16(0xFFFF);            // +0x06
        w.WriteI16(Stats.Hp);          // +0x08
        w.WriteI16(Stats.Stam);        // +0x0A
        w.WriteI16(Stats.Psi);         // +0x0C
        w.WriteI16(Stats.Conc);        // +0x0E
        w.WriteU32(Stats.Uc);          // +0x10
        w.WriteU32(Stats.Xp);          // +0x14
        w.WriteU32(Stats.Bdgt);        // +0x18
        w.WriteU32LittleEndian(Stats.Pp); // +0x1C  (little-endian)
        w.WriteU32(AppearanceCode);    // +0x20
        w.WriteU32(PlayerId);          // +0x24  (echoed player id)
        w.WriteU8((byte)World);        // +0x28
        w.WriteU8(AptTier);            // +0x29
        w.WriteU16(0);                 // +0x2A
        for (int i = 0; i < 10; i++)   // +0x2C..+0x53  (reserved)
        {
            w.WriteU32(0);
        }
        w.WriteU16(0);                 // +0x54
        w.WriteI16(0);                 // +0x56
        w.WriteU16(1);                 // +0x58
        w.WriteFixedCString(Name, NameLength);              // +0x5A
        w.WriteFixedCString(Tag, TagLength);                // +0x6E
        w.WriteFixedCString(Description, DescriptionLength); // +0x72..+0x273
    }
}

/// <summary><see cref="PacketId.ENTER_WORLD"/> — world handoff (stub <c>send_enter_world</c>).</summary>
public sealed record EnterWorld(uint Status, WorldId World, ushort Node) : IServerMessage
{
    public PacketId Id => PacketId.ENTER_WORLD;

    public void WriteBody(PacketWriter w)
    {
        w.WriteU32(Status);
        w.WriteU32((uint)World);
        w.WriteU16(Node);
        w.WriteU16(0);
    }
}

/// <summary><see cref="PacketId.PING"/> reply — echoes the client's timestamp (stub <c>pong</c>).</summary>
public sealed record Pong(uint Timestamp) : IServerMessage
{
    public PacketId Id => PacketId.PING;

    public void WriteBody(PacketWriter w) => w.WriteU32(Timestamp);
}

/// <summary>
/// <see cref="PacketId.CHAT"/> broadcast (stub <c>build_chat</c>). Name occupies a
/// fixed 28-byte field; the message is Latin-1, truncated to 250 bytes, then
/// NUL-terminated.
/// </summary>
public sealed record ChatBroadcast(uint SenderId, ushort Channel, string Name, string Message) : IServerMessage
{
    public const int NameFieldLength = 28;   // CHAT_MSG_OFFSET(0x24) - CHAT_NAME_OFFSET(0x08)
    public const int MaxMessageBytes = 250;

    public PacketId Id => PacketId.CHAT;

    public void WriteBody(PacketWriter w)
    {
        w.WriteU32(SenderId);
        w.WriteU16(Channel);
        w.WriteU16((ushort)Message.Length);
        w.WriteFixedCString(Name, NameFieldLength);
        byte[] message = Encoding.Latin1.GetBytes(Message);
        w.WriteBytes(message.AsSpan(0, Math.Min(message.Length, MaxMessageBytes)));
        w.WriteU8(0);
    }
}
