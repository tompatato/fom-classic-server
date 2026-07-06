namespace FOM.Protocol.Messages;

/// <summary>
/// <see cref="PacketId.ZONE_UPDATE"/> (0x082D) — the zone/roster update carrying
/// player entries. Layout (confirmed against the CShell.dll handler class, ctor
/// FUN_100a5970 / vtable PTR_FUN_100fa8fc):
/// <code>
///   +0x000  entry[80]          header/self entry (see EntrySize)
///   +0x050  u16   count        number of valid entries in the array
///   +0x052  u16   pad
///   +0x054  entry[50][80]      object array (count-bounded)
///   +0xFF4  cstring            trailing NUL-terminated string
/// </code>
/// Each 80-byte entry is 7 big-endian u32 fields (id, appearance, …) + a 52-byte
/// name. Fixed 4085-byte body.
/// <para>
/// The reference stub sent <c>count = 0</c> with a zeroed array, so the client had
/// nothing to spawn (explains the null live result). This builder puts one
/// populated entry in <c>object[0]</c> and sets <c>count = 1</c>.
/// </para>
/// </summary>
public sealed record SpawnZoneUpdate(uint EntityId, string Name, uint AppearanceCode, ushort Count = 1)
    : IServerMessage
{
    /// <summary>The appearance code the reference stub used for the injected entity.</summary>
    public const uint DefaultAppearance = 0x71088820;

    public const int EntrySize = 80;      // 7× u32 (big-endian) + name[52]
    public const int EntryU32Fields = 7;
    public const int EntryNameLength = 52;
    public const int ObjectSlots = 50;
    public const int CountOffset = EntrySize;               // 0x50
    public const int ObjectArrayOffset = EntrySize + 4;     // 0x54 (after count + pad)
    public const int BodyLength = EntrySize + 4 + (ObjectSlots * EntrySize) + 1; // 4085

    public PacketId Id => PacketId.ZONE_UPDATE;

    public void WriteBody(PacketWriter w)
    {
        WriteEntry(w);                 // +0x000 header/self entry
        w.WriteU16(Count);             // +0x050 valid-entry count
        w.WriteU16(0);                 // +0x052 pad
        WriteEntry(w);                 // +0x054 object[0] — the entity to spawn
        w.WriteBytes(new byte[(ObjectSlots - 1) * EntrySize]); // object[1..49] empty
        w.WriteU8(0);                  // +0xFF4 trailing string
    }

    // One 80-byte entry: id, appearance, then reserved u32s, then a 52-byte name.
    private void WriteEntry(PacketWriter w)
    {
        w.WriteU32(EntityId);
        w.WriteU32(AppearanceCode);
        for (int i = 2; i < EntryU32Fields; i++)
        {
            w.WriteU32(0);
        }
        w.WriteFixedCString(Name, EntryNameLength);
    }
}
