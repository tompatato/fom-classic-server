using System.Buffers.Binary;
using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Protocol.Tests;

public class SpawnZoneUpdateTests
{
    [Fact]
    public void HasFixedBodyAndPlacesKeyFields()
    {
        var spawn = new SpawnZoneUpdate(4242, "CLONE", SpawnZoneUpdate.DefaultAppearance);
        byte[] frame = spawn.ToFrame();

        Assert.Equal(4085, SpawnZoneUpdate.BodyLength);
        Assert.Equal(PacketFrame.HeaderSize + 4085, frame.Length);

        Assert.True(PacketFrame.TryRead(frame, out ushort opcode, out ReadOnlySpan<byte> body, out _));
        Assert.Equal((ushort)PacketId.ZONE_UPDATE, opcode);

        // Header/self entry at +0x00.
        Assert.Equal(4242u, BinaryPrimitives.ReadUInt32BigEndian(body));            // +0x00 entity id
        Assert.Equal(0x71088820u, BinaryPrimitives.ReadUInt32BigEndian(body[4..])); // +0x04 appearance
        var name = new PacketReader(body[0x1C..]);
        Assert.Equal("CLONE", name.ReadFixedCString(SpawnZoneUpdate.EntryNameLength));

        // The fix: count = 1 at +0x50, and object[0] populated at +0x54.
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(body[SpawnZoneUpdate.CountOffset..]));
        Assert.Equal(4242u, BinaryPrimitives.ReadUInt32BigEndian(body[SpawnZoneUpdate.ObjectArrayOffset..]));
    }
}
