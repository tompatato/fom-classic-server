using FOM.Protocol;

namespace FOM.Protocol.Tests;

public class FrameBufferTests
{
    [Fact]
    public void YieldsNothingUntilAFullFrameArrives()
    {
        var fb = new FrameBuffer();
        fb.Append([0x07, 0xE5, 0x00, 0x04, 0x11, 0x22]); // header says 4 body bytes, only 2 here
        Assert.False(fb.TryReadFrame(out _, out _));

        fb.Append([0x33, 0x44]); // the rest
        Assert.True(fb.TryReadFrame(out ushort opcode, out byte[] body));
        Assert.Equal((ushort)PacketId.PING, opcode);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, body);
        Assert.False(fb.TryReadFrame(out _, out _));
    }

    [Fact]
    public void SplitsMultipleCoalescedFrames()
    {
        var fb = new FrameBuffer();
        // two frames in one read: PING(2 body) then ENTER_WORLD(0 body)
        fb.Append([0x07, 0xE5, 0x00, 0x02, 0xAA, 0xBB, 0x03, 0xEB, 0x00, 0x00]);

        Assert.True(fb.TryReadFrame(out ushort op1, out byte[] b1));
        Assert.Equal((ushort)PacketId.PING, op1);
        Assert.Equal(new byte[] { 0xAA, 0xBB }, b1);

        Assert.True(fb.TryReadFrame(out ushort op2, out byte[] b2));
        Assert.Equal((ushort)PacketId.ENTER_WORLD, op2);
        Assert.Empty(b2);

        Assert.False(fb.TryReadFrame(out _, out _));
        Assert.Equal(0, fb.Buffered);
    }

    [Fact]
    public void GrowsBeyondInitialCapacity()
    {
        var fb = new FrameBuffer(initialCapacity: 8);
        byte[] big = new byte[1000];
        byte[] frame = PacketFrame.Encode(PacketId.CHAT, big);
        // feed it one byte at a time to exercise partial accumulation + growth
        foreach (byte b in frame)
        {
            fb.Append([b]);
        }
        Assert.True(fb.TryReadFrame(out ushort opcode, out byte[] body));
        Assert.Equal((ushort)PacketId.CHAT, opcode);
        Assert.Equal(1000, body.Length);
    }
}
