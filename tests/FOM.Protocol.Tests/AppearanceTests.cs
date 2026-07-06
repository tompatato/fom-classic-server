using FOM.Protocol;

namespace FOM.Protocol.Tests;

public class AppearanceTests
{
    [Fact]
    public void Pack_MatchesKnownWireCode()
    {
        // The reference stub hardcodes this exact code for its default character
        // (rank 7, faction 1, male, all limbs/head 1, model 0) as 0x71088820.
        uint code = Appearance.Pack(
            rank: 7, faction: 1, female: false,
            leg: 1, arm: 1, torso: 1, head: 1, model: 0);

        Assert.Equal(0x71088820u, code);
    }

    [Fact]
    public void Unpack_RoundTrips()
    {
        AppearanceFields fields = Appearance.Unpack(0x71088820u);
        Assert.Equal(new AppearanceFields(
            Rank: 7, Faction: 1, Female: false,
            Leg: 1, Arm: 1, Torso: 1, Head: 1, Model: 0), fields);

        Assert.Equal(0x71088820u, Appearance.Pack(
            fields.Rank, fields.Faction, fields.Female,
            fields.Leg, fields.Arm, fields.Torso, fields.Head, fields.Model));
    }

    [Fact]
    public void Fields_AreMaskedToTheirBitWidths()
    {
        // Female flag set; head uses 6 bits, model 5 bits — overflowing values wrap.
        uint code = Appearance.Pack(
            rank: 0, faction: 0, female: true,
            leg: 0, arm: 0, torso: 0, head: 0, model: 0);
        Assert.Equal(1u << 23, code);
    }
}
