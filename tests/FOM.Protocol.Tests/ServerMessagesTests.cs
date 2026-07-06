using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Protocol.Tests;

public class ServerMessagesTests
{
    [Fact]
    public void Pong_EchoesTimestamp()
    {
        byte[] frame = new Pong(0x12345678).ToFrame();
        Assert.Equal(new byte[] { 0x07, 0xE5, 0x00, 0x04, 0x12, 0x34, 0x56, 0x78 }, frame);
    }

    [Fact]
    public void EnterWorld_MatchesStubLayout()
    {
        byte[] frame = new EnterWorld(4, WorldId.StsGenesis, 1).ToFrame();
        Assert.Equal(new byte[]
        {
            0x03, 0xEB, 0x00, 0x0C,
            0x00, 0x00, 0x00, 0x04, // status
            0x00, 0x00, 0x00, 0x15, // world 21
            0x00, 0x01,             // node
            0x00, 0x00,
        }, frame);
    }

    [Fact]
    public void ChatBroadcast_MatchesStubLayout()
    {
        byte[] frame = new ChatBroadcast(SenderId: 0x2A, Channel: 3, Name: "Neo", Message: "hi").ToFrame();

        var expected = new List<byte> { 0x03, 0xEA, 0x00, 0x27 }; // opcode CHAT, len=39
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x2A }); // sender id
        expected.AddRange(new byte[] { 0x00, 0x03 });             // channel
        expected.AddRange(new byte[] { 0x00, 0x02 });             // message length
        byte[] name = new byte[28];                               // 28-byte NUL-padded name
        "Neo"u8.CopyTo(name);
        expected.AddRange(name);
        expected.AddRange(new byte[] { 0x68, 0x69, 0x00 });       // "hi\0"

        Assert.Equal(expected.ToArray(), frame);
    }

    [Fact]
    public void LoginReturn_HasFixedSizeAndPlacesTrickyFieldsCorrectly()
    {
        var msg = new LoginReturn(
            HeaderId: LoginReturn.DefaultHeaderId,
            Status: 6,
            Stats: new LoginStats(Hp: 100, Stam: 100, Psi: 100, Conc: 100, Uc: 1000, Xp: 100, Bdgt: 0, Pp: 10),
            AppearanceCode: 0x71088820,
            PlayerId: 0x0000ABCD,
            World: WorldId.StsGenesis,
            AptTier: 1,
            Name: "Neo",
            Tag: "",
            Description: "");

        byte[] frame = msg.ToFrame();

        // 620-byte body + 4-byte header.
        Assert.Equal(4 + 620, frame.Length);

        Assert.True(PacketFrame.TryRead(frame, out ushort opcode, out ReadOnlySpan<byte> body, out _));
        Assert.Equal((ushort)PacketId.LOGIN_RETURN, opcode);

        // pp is little-endian at +0x1C ...
        Assert.Equal(new byte[] { 0x0A, 0x00, 0x00, 0x00 }, body.Slice(0x1C, 4).ToArray());
        // ... appearance big-endian at +0x20 ...
        Assert.Equal(new byte[] { 0x71, 0x08, 0x88, 0x20 }, body.Slice(0x20, 4).ToArray());
        // ... player id big-endian at +0x24 ...
        Assert.Equal(new byte[] { 0x00, 0x00, 0xAB, 0xCD }, body.Slice(0x24, 4).ToArray());
        // ... world byte at +0x28 ...
        Assert.Equal((byte)WorldId.StsGenesis, body[0x28]);
        // ... and the name field at +0x5A.
        var reader = new PacketReader(body[0x5A..]);
        Assert.Equal("Neo", reader.ReadFixedCString(LoginReturn.NameLength));
    }
}
