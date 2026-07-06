using FOM.Protocol;

namespace FOM.Protocol.Tests;

public class PacketWriterTests
{
    [Fact]
    public void WriteU16_IsBigEndian()
    {
        byte[] bytes = new PacketWriter().WriteU16(0x07D1).ToArray();
        Assert.Equal(new byte[] { 0x07, 0xD1 }, bytes);
    }

    [Fact]
    public void WriteU32_IsBigEndian()
    {
        byte[] bytes = new PacketWriter().WriteU32(1000).ToArray();
        Assert.Equal(new byte[] { 0x00, 0x00, 0x03, 0xE8 }, bytes);
    }

    [Fact]
    public void WriteU32LittleEndian_IsTheLeQuirk()
    {
        // login-return `pp` is the one little-endian u32 on the wire.
        byte[] bytes = new PacketWriter().WriteU32LittleEndian(10).ToArray();
        Assert.Equal(new byte[] { 0x0A, 0x00, 0x00, 0x00 }, bytes);
    }

    [Theory]
    [InlineData((short)-1, new byte[] { 0xFF, 0xFF })]
    [InlineData((short)100, new byte[] { 0x00, 0x64 })]
    public void WriteI16_IsSignedBigEndian(short value, byte[] expected)
    {
        Assert.Equal(expected, new PacketWriter().WriteI16(value).ToArray());
    }

    [Fact]
    public void WriteFixedCString_PadsWithNul()
    {
        byte[] bytes = new PacketWriter().WriteFixedCString("AB", 4).ToArray();
        Assert.Equal(new byte[] { 0x41, 0x42, 0x00, 0x00 }, bytes);
    }

    [Fact]
    public void WriteFixedCString_TruncatesWithoutTerminatorWhenFull()
    {
        // "TOOLONG"[:4] == "TOOL"; a field-filling string gets no NUL.
        byte[] bytes = new PacketWriter().WriteFixedCString("TOOLONG", 4).ToArray();
        Assert.Equal(new byte[] { 0x54, 0x4F, 0x4F, 0x4C }, bytes);
    }

    [Fact]
    public void ToFrame_PrependsBigEndianOpcodeAndLength()
    {
        // send_enter_world(status=4, world=StsGenesis(21), node=1):
        //   u32 status | u32 world | u16 node | u16 0
        byte[] frame = new PacketWriter()
            .WriteU32(4)
            .WriteU32((uint)WorldId.StsGenesis)
            .WriteU16(1)
            .WriteU16(0)
            .ToFrame(PacketId.ENTER_WORLD);

        Assert.Equal(new byte[]
        {
            0x03, 0xEB,             // opcode ENTER_WORLD
            0x00, 0x0C,             // length = 12
            0x00, 0x00, 0x00, 0x04, // status
            0x00, 0x00, 0x00, 0x15, // world 21
            0x00, 0x01,             // node
            0x00, 0x00,             // pad
        }, frame);
    }
}
