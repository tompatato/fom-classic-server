using System.Buffers.Binary;
using FOM.Protocol.Messages;

namespace FOM.Protocol.Tests;

public class CharacterSnapshotTests
{
    [Fact]
    public void SingleEntry_PlacesCountAndEntryAtWalkerOffsets()
    {
        var entry = new SnapshotEntry(EntityId: 0x00ABCDEF, X: 0x1122, Y: 0x3344, Z: 0x5566, Appearance: 0x71088820);
        byte[] dg = CharacterSnapshot.Build(entry);

        // Datagram is header (0x18) + one 32-byte entry.
        Assert.Equal(CharacterSnapshot.EntriesOffset + CharacterSnapshot.EntryStride, dg.Length);

        // Header: candidate opcode big-endian, then the body length.
        Assert.Equal(CharacterSnapshot.CandidateOpcode, BinaryPrimitives.ReadUInt16BigEndian(dg));
        Assert.Equal(dg.Length - 4, BinaryPrimitives.ReadUInt16BigEndian(dg.AsSpan(2)));

        // count @ +0x14, little-endian.
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(dg.AsSpan(CharacterSnapshot.CountOffset)));

        // Entry fields @ +0x18, little-endian, at their walker offsets.
        Span<byte> e = dg.AsSpan(CharacterSnapshot.EntriesOffset, CharacterSnapshot.EntryStride);
        Assert.Equal(0x00ABCDEFu, BinaryPrimitives.ReadUInt32LittleEndian(e));            // +0x00 id, type nibble 0
        Assert.Equal(0x1122u | (0x5566u << 16), BinaryPrimitives.ReadUInt32LittleEndian(e[0x04..])); // X | Z<<16
        Assert.Equal(0x3344u, BinaryPrimitives.ReadUInt32LittleEndian(e[0x08..]));        // +0x08 Y
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(e[0x0C..]));             // +0x0C flags
        Assert.Equal(0x71088820u, BinaryPrimitives.ReadUInt32LittleEndian(e[0x1C..]));    // +0x1C appearance
    }

    [Fact]
    public void TypeNibbleAndId_PackIntoOffsetZero()
    {
        var entry = new SnapshotEntry(EntityId: 0xFF123456, X: 0, Y: 0, Z: 0, Appearance: 0) { Type = 0x3 };
        byte[] dg = CharacterSnapshot.Build(entry);

        // id masked to 24 bits; type nibble in bits 24-27 (< 0xB => character).
        uint word = BinaryPrimitives.ReadUInt32LittleEndian(dg.AsSpan(CharacterSnapshot.EntriesOffset));
        Assert.Equal(0x00123456u, word & 0x00FFFFFF);
        Assert.Equal(0x3u, (word >> 24) & 0xF);
    }

    [Fact]
    public void MultipleEntries_AreContiguousAtStride()
    {
        var entries = new[]
        {
            new SnapshotEntry(1, 10, 20, 30, 0x71088820),
            new SnapshotEntry(2, 40, 50, 60, 0x71088821),
            new SnapshotEntry(3, 70, 80, 90, 0x71088822),
        };
        byte[] dg = CharacterSnapshot.Build(entries);

        Assert.Equal(CharacterSnapshot.EntriesOffset + entries.Length * CharacterSnapshot.EntryStride, dg.Length);
        Assert.Equal(entries.Length, BinaryPrimitives.ReadUInt16LittleEndian(dg.AsSpan(CharacterSnapshot.CountOffset)));

        for (int i = 0; i < entries.Length; i++)
        {
            int at = CharacterSnapshot.EntriesOffset + i * CharacterSnapshot.EntryStride;
            Assert.Equal(entries[i].EntityId, BinaryPrimitives.ReadUInt32LittleEndian(dg.AsSpan(at)) & 0x00FFFFFF);
        }
    }

    [Fact]
    public void MoreThanFiftyEntries_Throws()
    {
        var entries = new SnapshotEntry[CharacterSnapshot.MaxEntries + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() => CharacterSnapshot.Build(entries));
    }
}
