namespace FOM.Protocol;

/// <summary>
/// World/colony identifiers. The client reaches each world on its own TCP+UDP
/// port, where <c>port = 7500 + (int)WorldId</c> (so GroundZero → 7500). Names
/// keep their in-game proper-noun casing.
/// </summary>
public enum WorldId
{
    // Earth
    GroundZero = 0,
    Manhattan = 1,
    Brooklyn = 2,
    Shibuya = 3,
    ChuoKu = 4,
    Kamitakada = 5,
    Otaku = 6,
    ParisAdt = 7,
    ParisNd = 8,
    BlnAlex = 9,
    BlnTier = 10,

    // Colonies
    Andromeda = 11,
    SolsOutpost = 12,
    NewHaven = 13,
    Arcturus = 14,
    Ganymede = 15,
    TitanStation = 16,
    CeresDelta = 17,
    DeMorgan = 18,
    KeplersDome = 19,
    MoonBase = 20,
    StsGenesis = 21,
    GdsYukon = 22,
    BookersValley = 23,
    EpsilonEridani = 24,
    TerraVenture1 = 25,
    DominionExodus = 26,
    EspenParadise = 27,
    Aquatica = 28,
    Pegasi51 = 29,
    Constantinople = 30,
    Aurelia = 31,
    PaxPrime = 32,

    Cloning = 40,
}

/// <summary>Maps <see cref="WorldId"/> to/from its TCP+UDP port.</summary>
public static class WorldPort
{
    public const int FirstPort = 7500;

    public static int ForWorld(WorldId world) => FirstPort + (int)world;

    public static WorldId FromPort(int port) => (WorldId)(port - FirstPort);
}
