namespace FOM.Protocol.Messages;

/// <summary>
/// The character stat block carried in <see cref="LoginReturn"/>. Health/stamina/
/// psi/concentration are signed 16-bit; the currency/xp/budget are unsigned 32-bit
/// big-endian; <see cref="Pp"/> is the one little-endian field on the wire.
/// </summary>
public readonly record struct LoginStats(
    short Hp,
    short Stam,
    short Psi,
    short Conc,
    uint Uc,
    uint Xp,
    uint Bdgt,
    uint Pp);
