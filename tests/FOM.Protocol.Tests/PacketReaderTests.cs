using FOM.Protocol;

namespace FOM.Protocol.Tests;

public class PacketReaderTests
{
    [Fact]
    public void ReadsBigEndianPrimitives()
    {
        var reader = new PacketReader([0x07, 0xD1, 0x00, 0x00, 0x03, 0xE8, 0xFF, 0xFF]);
        Assert.Equal(0x07D1, reader.ReadU16());
        Assert.Equal(1000u, reader.ReadU32());
        Assert.Equal(-1, reader.ReadI16());
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ReadU32LittleEndian_MatchesTheLeQuirk()
    {
        var reader = new PacketReader([0x0A, 0x00, 0x00, 0x00]);
        Assert.Equal(10u, reader.ReadU32LittleEndian());
    }

    [Fact]
    public void ReadFixedCString_StopsAtNul()
    {
        // LOGIN_REQUEST carries the name in a 20-byte NUL-padded field.
        byte[] field = new byte[20];
        "Neo"u8.CopyTo(field);
        var reader = new PacketReader(field);
        Assert.Equal("Neo", reader.ReadFixedCString(20));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void RoundTripsWriterOutput()
    {
        byte[] body = new PacketWriter()
            .WriteU32(12345)
            .WriteU16(6)
            .WriteFixedCString("Trinity", 20)
            .WriteU32LittleEndian(10)
            .ToArray();

        var reader = new PacketReader(body);
        Assert.Equal(12345u, reader.ReadU32());
        Assert.Equal(6, reader.ReadU16());
        Assert.Equal("Trinity", reader.ReadFixedCString(20));
        Assert.Equal(10u, reader.ReadU32LittleEndian());
    }

    [Fact]
    public void OverReadThrows()
    {
        Assert.Throws<EndOfStreamException>(() =>
        {
            var reader = new PacketReader([0x00, 0x01]);
            reader.ReadU32();
        });
    }
}
