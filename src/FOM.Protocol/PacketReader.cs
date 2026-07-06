using System.Buffers.Binary;
using System.Text;

namespace FOM.Protocol;

/// <summary>
/// Reads a packet body written by <see cref="PacketWriter"/> / the wire.
/// Big-endian by default; <see cref="ReadU32LittleEndian"/> is the one LE field.
/// Over-reads throw <see cref="EndOfStreamException"/>.
/// </summary>
public ref struct PacketReader(ReadOnlySpan<byte> body)
{
    private readonly ReadOnlySpan<byte> _body = body;
    private int _pos = 0;

    public readonly int Position => _pos;
    public readonly int Remaining => _body.Length - _pos;

    private ReadOnlySpan<byte> Take(int n)
    {
        if (_pos + n > _body.Length)
        {
            throw new EndOfStreamException(
                $"read of {n} byte(s) at offset {_pos} exceeds body length {_body.Length}");
        }
        ReadOnlySpan<byte> slice = _body.Slice(_pos, n);
        _pos += n;
        return slice;
    }

    public byte ReadU8() => Take(1)[0];

    public ushort ReadU16() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    public short ReadI16() => BinaryPrimitives.ReadInt16BigEndian(Take(2));

    public uint ReadU32() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    /// <summary>The lone little-endian field on the wire (login-return <c>pp</c>).</summary>
    public uint ReadU32LittleEndian() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    public ReadOnlySpan<byte> ReadBytes(int n) => Take(n);

    /// <summary>
    /// Reads a fixed <paramref name="length"/>-byte field and decodes up to the
    /// first NUL as Latin-1 (matches the reference readers' <c>.decode("latin1")</c>).
    /// Note the deliberate asymmetry with <see cref="PacketWriter.WriteFixedCString"/>,
    /// which encodes UTF-8; the two agree for ASCII, which is all the client sends.
    /// </summary>
    public string ReadFixedCString(int length)
    {
        ReadOnlySpan<byte> field = Take(length);
        int nul = field.IndexOf((byte)0);
        ReadOnlySpan<byte> text = nul >= 0 ? field[..nul] : field;
        return Encoding.Latin1.GetString(text);
    }
}
