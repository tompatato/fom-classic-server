namespace FOM.Protocol;

/// <summary>
/// Packs/unpacks the 32-bit character appearance code carried in
/// <see cref="PacketId.LOGIN_RETURN"/> (and spawn entries). Bit layout,
/// most-significant first:
/// <code>
///   rank    &lt;&lt; 28  (4 bits)
///   faction &lt;&lt; 24  (4 bits)
///   female  &lt;&lt; 23  (1 bit)
///   leg     &lt;&lt; 19  (4 bits)
///   arm     &lt;&lt; 15  (4 bits)
///   torso   &lt;&lt; 11  (4 bits)
///   head    &lt;&lt;  5  (6 bits)
///   model            (5 bits)
/// </code>
/// </summary>
public static class Appearance
{
    public static uint Pack(
        int rank,
        int faction,
        bool female,
        int leg,
        int arm,
        int torso,
        int head,
        int model)
    {
        return ((uint)(rank & 0x0F) << 28)
             | ((uint)(faction & 0x0F) << 24)
             | ((female ? 1u : 0u) << 23)
             | ((uint)(leg & 0x0F) << 19)
             | ((uint)(arm & 0x0F) << 15)
             | ((uint)(torso & 0x0F) << 11)
             | ((uint)(head & 0x3F) << 5)
             | (uint)(model & 0x1F);
    }

    public static AppearanceFields Unpack(uint code) => new(
        Rank: (int)((code >> 28) & 0x0F),
        Faction: (int)((code >> 24) & 0x0F),
        Female: ((code >> 23) & 0x01) != 0,
        Leg: (int)((code >> 19) & 0x0F),
        Arm: (int)((code >> 15) & 0x0F),
        Torso: (int)((code >> 11) & 0x0F),
        Head: (int)((code >> 5) & 0x3F),
        Model: (int)(code & 0x1F));
}

public readonly record struct AppearanceFields(
    int Rank,
    int Faction,
    bool Female,
    int Leg,
    int Arm,
    int Torso,
    int Head,
    int Model);
