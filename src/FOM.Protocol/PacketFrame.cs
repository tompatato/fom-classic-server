using System.Buffers.Binary;

namespace FOM.Protocol;

/// <summary>
/// The TCP frame codec: <c>[opcode: u16 BE][length: u16 BE][body]</c>. The length
/// field counts body bytes only (max 65535), so a frame is <c>4 + length</c> bytes.
/// </summary>
public static class PacketFrame
{
    /// <summary>Fixed header size in bytes (opcode + length).</summary>
    public const int HeaderSize = 4;

    /// <summary>Largest body the u16 length field can express.</summary>
    public const int MaxBodyLength = ushort.MaxValue;

    /// <summary>Builds a complete frame from an opcode and body.</summary>
    public static byte[] Encode(ushort opcode, ReadOnlySpan<byte> body)
    {
        if (body.Length > MaxBodyLength)
        {
            throw new ArgumentOutOfRangeException(nameof(body),
                $"body length {body.Length} exceeds the {MaxBodyLength}-byte frame limit");
        }

        byte[] frame = new byte[HeaderSize + body.Length];
        BinaryPrimitives.WriteUInt16BigEndian(frame, opcode);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), (ushort)body.Length);
        body.CopyTo(frame.AsSpan(HeaderSize));
        return frame;
    }

    /// <inheritdoc cref="Encode(ushort, ReadOnlySpan{byte})"/>
    public static byte[] Encode(PacketId opcode, ReadOnlySpan<byte> body) => Encode((ushort)opcode, body);

    /// <summary>
    /// Tries to parse one frame from the front of <paramref name="buffer"/> (a TCP
    /// receive buffer that may hold a partial or several frames). Returns
    /// <see langword="false"/> when more bytes are needed; on success,
    /// <paramref name="body"/> slices into <paramref name="buffer"/> and
    /// <paramref name="consumed"/> is the whole frame's byte count.
    /// </summary>
    public static bool TryRead(
        ReadOnlySpan<byte> buffer,
        out ushort opcode,
        out ReadOnlySpan<byte> body,
        out int consumed)
    {
        opcode = 0;
        body = default;
        consumed = 0;

        if (buffer.Length < HeaderSize)
        {
            return false;
        }

        ushort length = BinaryPrimitives.ReadUInt16BigEndian(buffer[2..]);
        int total = HeaderSize + length;
        if (buffer.Length < total)
        {
            return false;
        }

        opcode = BinaryPrimitives.ReadUInt16BigEndian(buffer);
        body = buffer.Slice(HeaderSize, length);
        consumed = total;
        return true;
    }
}
