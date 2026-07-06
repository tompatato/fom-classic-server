namespace FOM.Protocol.Messages;

/// <summary>
/// <see cref="PacketId.ZONE_UPDATE"/> (0x082D) carrying a single player entry — the
/// candidate "spawn another player" packet. Layout ported from the reference stub's
/// <c>build_spawn_082d</c>:
/// <code>
///   +0x000  u32   entity id (must match the entity's movement session id)
///   +0x004  u32   appearance code
///   +0x008  5× u32 (reserved / unknown, zeroed)   -> through +0x01C
///   +0x01C  char[52] name/flags
///   +0x050  u16   object count (= 0)
///   +0x052  u16   pad
///   +0x054  50 × 80-byte object slots (zeroed)     -> through +0xFF4
///   +0xFF4  u8    trailing NUL
/// </code>
/// Body is a fixed 4085 bytes.
/// <para>
/// ⚠️ UNVERIFIED: the stub author noted this may populate a UI roster rather than
/// spawn a 3D avatar. Kept as an experiment (see the spawn-injection path in the
/// server) until confirmed against the live client.
/// </para>
/// </summary>
public sealed record SpawnZoneUpdate(uint EntityId, string Name, uint AppearanceCode) : IServerMessage
{
    /// <summary>The appearance code the reference stub used for the injected entity.</summary>
    public const uint DefaultAppearance = 0x71088820;

    public const int NameLength = 52;
    public const int ObjectSlots = 50;
    public const int ObjectSlotSize = 80;
    public const int BodyLength = 0x54 + (ObjectSlots * ObjectSlotSize) + 1; // 4085

    public PacketId Id => PacketId.ZONE_UPDATE;

    public void WriteBody(PacketWriter w)
    {
        w.WriteU32(EntityId);         // +0x00
        w.WriteU32(AppearanceCode);   // +0x04
        for (int i = 0; i < 5; i++)   // +0x08 .. +0x1C  (reserved)
        {
            w.WriteU32(0);
        }
        w.WriteFixedCString(Name, NameLength);      // +0x1C
        w.WriteU16(0);                              // +0x50  object count
        w.WriteU16(0);                              // +0x52  pad
        w.WriteBytes(new byte[ObjectSlots * ObjectSlotSize]); // +0x54  object array (empty)
        w.WriteU8(0);                               // +0xFF4 trailing
    }
}
