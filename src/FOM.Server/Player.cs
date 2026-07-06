using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Server;

/// <summary>Server-side state for one logged-in player.</summary>
public sealed class Player(uint id, ClientSession session)
{
    /// <summary>
    /// Unique server-assigned id. Sent as the <see cref="Messages.LoginReturn"/>
    /// header field (+0x00) and echoed by the client as its movement session id
    /// (confirmed by live capture: what we send there comes back in 0x03F3) — so it
    /// keys incoming movement back to this player.
    /// </summary>
    public uint Id { get; } = id;

    public ClientSession Session { get; } = session;

    public string Name { get; set; } = "Player";

    /// <summary>The world the player is in (derived from the port they connected on).</summary>
    public WorldId World { get; set; }

    /// <summary>Most recent position/heading from the UDP movement channel, if any.</summary>
    public MovementUpdate? LastMovement { get; set; }
}
