using System.Text;

namespace FOM.Protocol.Messages;

/// <summary>
/// <see cref="PacketId.LOGIN_REQUEST"/> — carries the character name in a 20-byte
/// NUL-padded field at offset 0. (A port/build number is thought to follow, at an
/// offset not yet confirmed — 20 vs 32; parsed lazily once nailed down.)
/// </summary>
public readonly record struct LoginRequest(string Name)
{
    public static LoginRequest Parse(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new LoginRequest(r.ReadFixedCString(20).Trim());
    }
}

/// <summary><see cref="PacketId.CHAT"/> from the client (stub <c>parse_chat</c>).</summary>
public readonly record struct ChatMessage(uint SenderId, ushort Channel, string Name, string Message)
{
    /// <summary>Byte offset where the message text begins (after the 28-byte name).</summary>
    public const int MessageOffset = 0x24;

    public static ChatMessage Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < MessageOffset)
        {
            return new ChatMessage(0, 0, string.Empty, string.Empty);
        }

        var r = new PacketReader(body);
        uint senderId = r.ReadU32();
        ushort channel = r.ReadU16();
        ushort messageLength = r.ReadU16();
        string name = r.ReadFixedCString(ChatBroadcast.NameFieldLength);
        int take = Math.Min(messageLength, r.Remaining);
        string message = Encoding.Latin1.GetString(r.ReadBytes(take));
        return new ChatMessage(senderId, channel, name, message);
    }
}

/// <summary>
/// <see cref="PacketId.MOVEMENT"/> — the UDP position/heading update (stub
/// <c>decode_movement</c>). The datagram is itself framed
/// (<c>[opcode][len][body]</c>); fields are 16-bit big-endian.
/// </summary>
public readonly record struct MovementUpdate(
    uint Session,
    ushort X,
    ushort Y,
    ushort Z,
    ushort Velocity,
    ushort Heading,
    ushort VelocityFlag)
{
    /// <summary>Minimum datagram size to carry all mapped fields (through +0x14).</summary>
    public const int MinLength = 22;

    /// <summary>Heading expressed in degrees (the raw value spans a full u16).</summary>
    public double HeadingDegrees => Heading * 360.0 / 65536.0;

    /// <summary>Returns the parsed update, or <see langword="null"/> if this isn't a movement datagram.</summary>
    public static MovementUpdate? TryParse(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < MinLength ||
            datagram[0] != ((ushort)PacketId.MOVEMENT >> 8) ||
            datagram[1] != ((ushort)PacketId.MOVEMENT & 0xFF))
        {
            return null;
        }

        var r = new PacketReader(datagram);
        r.ReadU16();                 // opcode
        r.ReadU16();                 // length (unused)
        uint session = r.ReadU32();  // +0x04
        ushort x = r.ReadU16();      // +0x08
        ushort y = r.ReadU16();      // +0x0A (height)
        ushort z = r.ReadU16();      // +0x0C
        r.ReadU16();                 // +0x0E (unmapped)
        ushort velocity = r.ReadU16();     // +0x10
        ushort heading = r.ReadU16();      // +0x12
        ushort velocityFlag = r.ReadU16(); // +0x14
        return new MovementUpdate(session, x, y, z, velocity, heading, velocityFlag);
    }
}
