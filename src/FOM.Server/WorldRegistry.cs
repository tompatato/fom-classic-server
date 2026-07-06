using System.Collections.Concurrent;
using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Server;

/// <summary>
/// Tracks logged-in players and which world each is in. Thread-safe; the single
/// source of truth for who is online and where. Movement-broadcast, spawn/roster,
/// and persistence build on this.
/// </summary>
public sealed class WorldRegistry
{
    private readonly ConcurrentDictionary<uint, Player> _players = new();
    private int _nextId;

    /// <summary>Allocates a unique, non-zero player/session id.</summary>
    public uint AllocateId() => (uint)Interlocked.Increment(ref _nextId);

    public Player Add(Player player)
    {
        _players[player.Id] = player;
        return player;
    }

    public void Remove(uint id) => _players.TryRemove(id, out _);

    public bool TryGet(uint id, out Player? player) => _players.TryGetValue(id, out player);

    /// <summary>Players currently in the given world.</summary>
    public IReadOnlyList<Player> InWorld(WorldId world) =>
        _players.Values.Where(p => p.World == world).ToList();

    public IReadOnlyCollection<Player> All => _players.Values.ToList();

    public int Count => _players.Count;

    /// <summary>Records a movement update against its player, keyed by session id.
    /// Returns false if no player owns that id.</summary>
    public bool UpdatePosition(uint sessionId, MovementUpdate movement)
    {
        if (_players.TryGetValue(sessionId, out Player? player))
        {
            player.LastMovement = movement;
            return true;
        }
        return false;
    }
}
