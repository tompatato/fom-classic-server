namespace FOM.Protocol;

/// <summary>
/// Opcodes for the 2006 Face of Mankind wire protocol. On the TCP channel each
/// frame is <c>[PacketId: u16 BE][length: u16 BE][body]</c>; <see cref="MOVEMENT"/>
/// travels on the parallel UDP channel.
/// <para>
/// Derived against the 2006 client (see the reference server stub). Names ending
/// in <c>_UNCONFIRMED</c> are opcodes we have observed but not yet fully mapped.
/// </para>
/// </summary>
public enum PacketId : ushort
{
    // --- Session / login ---
    LOGIN_REQUEST = 0x07D1, // C->S  name + port + build
    LOGIN_RETURN  = 0x07D2, // S->C  stats, appearance, world, tag, description
    LOAD_CHAR     = 0x07DC, // C->S  character selected / loaded
    PING          = 0x07E5, // both  keepalive; echoes a timestamp

    // --- World / colony ---
    ENTER_WORLD   = 0x03EB, // S->C  world handoff
    EXIT_APT      = 0x081C, // C->S  vort out of apartment into the colony
    ZONE_UPDATE   = 0x082D, // S->C  zone/roster update (spawn entries)

    // --- Chat ---
    CHAT          = 0x03EA, // both  chat message (server relays to peers)

    // --- Movement (UDP channel) ---
    MOVEMENT      = 0x03F3, // C->S  position/heading update

    // --- Observed, not yet fully mapped ---
    NODE_REQ_UNCONFIRMED     = 0x03E9, // C->S  after colony entry (u32=76)
    POST_ENTER_UNCONFIRMED   = 0x0809, // C->S  one-shot after apt entry
    WORLD_LOADED_UNCONFIRMED = 0x081A, // C->S  right after ENTER_WORLD (u32=5)
    POLL_UNCONFIRMED         = 0x0822, // C->S  pre-login + every ~10s keepalive
    KEEPALIVE30_UNCONFIRMED  = 0x083B, // C->S  every ~30s
}
