using FOM.Protocol;

namespace FOM.Protocol.Tests;

public class PacketFrameTests
{
    [Fact]
    public void Encode_WritesBigEndianHeaderThenBody()
    {
        byte[] frame = PacketFrame.Encode(PacketId.ENTER_WORLD, [0xDE, 0xAD, 0xBE, 0xEF]);
        Assert.Equal(new byte[] { 0x03, 0xEB, 0x00, 0x04, 0xDE, 0xAD, 0xBE, 0xEF }, frame);
    }

    [Fact]
    public void TryRead_NeedsFullHeader()
    {
        Assert.False(PacketFrame.TryRead([0x03], out _, out _, out _));
    }

    [Fact]
    public void TryRead_NeedsFullBody()
    {
        // header claims 4 body bytes but only 2 are present
        byte[] partial = [0x03, 0xEB, 0x00, 0x04, 0xDE, 0xAD];
        Assert.False(PacketFrame.TryRead(partial, out _, out _, out _));
    }

    [Fact]
    public void TryRead_ParsesOneFrameAndReportsConsumed()
    {
        byte[] buffer = [0x03, 0xEB, 0x00, 0x02, 0xAA, 0xBB, /* next frame: */ 0x07, 0xE5];
        bool ok = PacketFrame.TryRead(buffer, out ushort opcode, out ReadOnlySpan<byte> body, out int consumed);

        Assert.True(ok);
        Assert.Equal((ushort)PacketId.ENTER_WORLD, opcode);
        Assert.Equal(new byte[] { 0xAA, 0xBB }, body.ToArray());
        Assert.Equal(6, consumed); // 4 header + 2 body; the trailing bytes remain
    }

    [Fact]
    public void EncodeThenTryRead_RoundTrips()
    {
        byte[] body = new PacketWriter().WriteU32(0x07E5).WriteFixedCString("hi", 4).ToArray();
        byte[] frame = PacketFrame.Encode(PacketId.PING, body);

        Assert.True(PacketFrame.TryRead(frame, out ushort opcode, out ReadOnlySpan<byte> parsed, out int consumed));
        Assert.Equal((ushort)PacketId.PING, opcode);
        Assert.Equal(body, parsed.ToArray());
        Assert.Equal(frame.Length, consumed);
    }
}
