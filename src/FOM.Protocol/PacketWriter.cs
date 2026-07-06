using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace FOM.Protocol;

/// <summary>
/// Builds a packet body. Multi-byte values are written <b>big-endian</b> to match
/// the wire, except <see cref="WriteU32LittleEndian"/> — the single known
/// little-endian field (login-return <c>pp</c>). Mirrors the reference stub's
/// <c>put_*</c> helpers so output is byte-for-byte identical.
/// </summary>
public sealed class PacketWriter
{
    private readonly ArrayBufferWriter<byte> _buffer = new();

    /// <summary>Bytes written to the body so far.</summary>
    public int Length => _buffer.WrittenCount;

    public PacketWriter WriteU8(byte value)
    {
        _buffer.GetSpan(1)[0] = value;
        _buffer.Advance(1);
        return this;
    }

    public PacketWriter WriteU16(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(_buffer.GetSpan(2), value);
        _buffer.Advance(2);
        return this;
    }

    public PacketWriter WriteI16(short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(_buffer.GetSpan(2), value);
        _buffer.Advance(2);
        return this;
    }

    public PacketWriter WriteU32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_buffer.GetSpan(4), value);
        _buffer.Advance(4);
        return this;
    }

    /// <summary>The lone little-endian field on the wire (login-return <c>pp</c>).</summary>
    public PacketWriter WriteU32LittleEndian(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.GetSpan(4), value);
        _buffer.Advance(4);
        return this;
    }

    public PacketWriter WriteBytes(ReadOnlySpan<byte> value)
    {
        value.CopyTo(_buffer.GetSpan(value.Length));
        _buffer.Advance(value.Length);
        return this;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as UTF-8, truncated to
    /// <paramref name="length"/> bytes and NUL-padded to exactly that length. A
    /// string that fills the whole field gets no terminator — matching the
    /// reference <c>put_fixed_cstring</c> (<c>s.encode()[:n] + b"\0" * pad</c>).
    /// </summary>
    public PacketWriter WriteFixedCString(string value, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Span<byte> field = _buffer.GetSpan(length)[..length];
        field.Clear();
        Span<byte> encoded = stackalloc byte[Encoding.UTF8.GetByteCount(value)];
        Encoding.UTF8.GetBytes(value, encoded);
        encoded[..Math.Min(encoded.Length, length)].CopyTo(field);
        _buffer.Advance(length);
        return this;
    }

    /// <summary>The body bytes written so far (a copy).</summary>
    public byte[] ToArray() => _buffer.WrittenSpan.ToArray();

    /// <summary>
    /// Wraps the body in a full frame: <c>[opcode: u16 BE][length: u16 BE][body]</c>.
    /// </summary>
    public byte[] ToFrame(ushort opcode) => PacketFrame.Encode(opcode, _buffer.WrittenSpan);

    /// <inheritdoc cref="ToFrame(ushort)"/>
    public byte[] ToFrame(PacketId opcode) => ToFrame((ushort)opcode);
}
