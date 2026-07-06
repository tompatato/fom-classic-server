using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Protocol.Tests;

public class ClientMessagesTests
{
    [Fact]
    public void LoginRequest_ReadsNameFromFixedField()
    {
        byte[] body = new byte[24];
        "Trinity"u8.CopyTo(body);
        Assert.Equal("Trinity", LoginRequest.Parse(body).Name);
    }

    [Fact]
    public void Chat_RoundTripsThroughServerBuilder()
    {
        // Build a chat frame, strip the header, and parse the body back.
        byte[] frame = new ChatBroadcast(SenderId: 7, Channel: 2, Name: "Morpheus", Message: "wake up").ToFrame();
        Assert.True(PacketFrame.TryRead(frame, out _, out ReadOnlySpan<byte> body, out _));

        ChatMessage parsed = ChatMessage.Parse(body);
        Assert.Equal(7u, parsed.SenderId);
        Assert.Equal(2, parsed.Channel);
        Assert.Equal("Morpheus", parsed.Name);
        Assert.Equal("wake up", parsed.Message);
    }

    [Fact]
    public void Chat_ReturnsEmptyForUndersizedBody()
    {
        ChatMessage parsed = ChatMessage.Parse([0x00, 0x01, 0x02]);
        Assert.Equal(string.Empty, parsed.Message);
        Assert.Equal(string.Empty, parsed.Name);
    }

    [Fact]
    public void Movement_ParsesFramedUdpDatagram()
    {
        // [opcode 03F3][len 1C][session][X][Y][Z][pad][vel][heading][velFlag]
        byte[] datagram =
        [
            0x03, 0xF3, 0x00, 0x1C,
            0x00, 0x00, 0x30, 0x39, // session = 12345
            0x01, 0x00,             // X
            0x02, 0x00,             // Y
            0x03, 0x00,             // Z
            0x00, 0x00,             // pad (+0x0E)
            0x00, 0x05,             // velocity
            0x40, 0x00,             // heading = 0x4000 -> 90 degrees
            0x00, 0x09,             // velocity flag
        ];

        MovementUpdate? parsed = MovementUpdate.TryParse(datagram);
        Assert.NotNull(parsed);
        MovementUpdate m = parsed.Value;
        Assert.Equal(12345u, m.Session);
        Assert.Equal(0x0100, m.X);
        Assert.Equal(0x0200, m.Y);
        Assert.Equal(0x0300, m.Z);
        Assert.Equal(5, m.Velocity);
        Assert.Equal(0x4000, m.Heading);
        Assert.Equal(90.0, m.HeadingDegrees, precision: 3);
        Assert.Equal(9, m.VelocityFlag);
    }

    [Theory]
    [InlineData(new byte[] { 0x07, 0xD1, 0x00, 0x00 })]        // wrong opcode
    [InlineData(new byte[] { 0x03, 0xF3, 0x00, 0x1C, 0x00 })]  // too short
    public void Movement_RejectsNonMovementDatagrams(byte[] datagram)
    {
        Assert.Null(MovementUpdate.TryParse(datagram));
    }
}
