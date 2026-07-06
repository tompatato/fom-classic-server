using FOM.Protocol;
using FOM.Protocol.Messages;
using FOM.Server;

namespace FOM.Server.Tests;

public class WorldRegistryTests
{
    private static Player NewPlayer(WorldRegistry reg, string name, WorldId world)
    {
        var player = new Player(reg.AllocateId(), session: null!) { Name = name, World = world };
        return reg.Add(player);
    }

    [Fact]
    public void AllocateId_IsUniqueAndNonZero()
    {
        var reg = new WorldRegistry();
        uint a = reg.AllocateId();
        uint b = reg.AllocateId();
        Assert.NotEqual(0u, a);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void InWorld_GroupsByWorld()
    {
        var reg = new WorldRegistry();
        NewPlayer(reg, "Alice", WorldId.StsGenesis);
        NewPlayer(reg, "Bob", WorldId.StsGenesis);
        NewPlayer(reg, "Carol", WorldId.Manhattan);

        Assert.Equal(2, reg.InWorld(WorldId.StsGenesis).Count);
        Assert.Single(reg.InWorld(WorldId.Manhattan));
        Assert.Empty(reg.InWorld(WorldId.Aquatica));
        Assert.Equal(3, reg.Count);
    }

    [Fact]
    public void Remove_DropsThePlayer()
    {
        var reg = new WorldRegistry();
        Player p = NewPlayer(reg, "Alice", WorldId.StsGenesis);
        reg.Remove(p.Id);
        Assert.Equal(0, reg.Count);
        Assert.False(reg.TryGet(p.Id, out _));
    }

    [Fact]
    public void UpdatePosition_RecordsAgainstSessionId()
    {
        var reg = new WorldRegistry();
        Player p = NewPlayer(reg, "Alice", WorldId.StsGenesis);
        var move = new MovementUpdate(Session: p.Id, X: 619, Y: 63432, Z: 63560, Velocity: 5, Heading: 46170, VelocityFlag: 9);

        Assert.True(reg.UpdatePosition(p.Id, move));
        Assert.True(reg.TryGet(p.Id, out Player? found));
        Assert.Equal(619, found!.LastMovement!.Value.X);

        Assert.False(reg.UpdatePosition(99999, move)); // unknown session id
    }
}
