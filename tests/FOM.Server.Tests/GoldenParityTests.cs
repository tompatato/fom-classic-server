using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Server.Tests;

/// <summary>
/// Asserts the C# builders reproduce, byte-for-byte, the fixtures generated from
/// the reference stub (tools/harness/gen_golden.py) — pinning the server to the
/// reverse-engineered ground truth. Regenerate the fixtures with that script.
/// </summary>
public class GoldenParityTests
{
    private static readonly Dictionary<string, string> Golden = LoadGolden();

    [Fact]
    public void Pong_MatchesGolden() =>
        AssertMatches("pong", new Pong(0x12345678).ToFrame());

    [Fact]
    public void EnterWorld_MatchesGolden() =>
        AssertMatches("enter_world", new EnterWorld(4, WorldId.StsGenesis, 1).ToFrame());

    [Fact]
    public void Chat_MatchesGolden() =>
        AssertMatches("chat", new ChatBroadcast(42, 3, "Neo", "hi").ToFrame());

    [Fact]
    public void LoginReturn_MatchesGolden()
    {
        var login = new LoginReturn(
            HeaderId: LoginReturn.DefaultHeaderId,
            Status: 6,
            Stats: new LoginStats(100, 100, 100, 100, 1000, 100, 0, 10),
            AppearanceCode: Appearance.Pack(7, 1, false, 1, 1, 1, 1, 0),
            PlayerId: 1001,
            World: WorldId.StsGenesis,
            AptTier: 1,
            Name: "Neo",
            Tag: "",
            Description: "");
        AssertMatches("login_return", login.ToFrame());
    }

    [Fact]
    public void Appearance_MatchesGolden()
    {
        byte[] raw = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(raw, Appearance.Pack(7, 1, false, 1, 1, 1, 1, 0));
        AssertMatches("appearance", raw);
    }

    private static void AssertMatches(string name, byte[] actual)
    {
        Assert.True(Golden.TryGetValue(name, out string? expected), $"no golden fixture named '{name}'");
        Assert.Equal(expected, Convert.ToHexString(actual));
    }

    private static Dictionary<string, string> LoadGolden()
    {
        Assembly asm = typeof(GoldenParityTests).Assembly;
        string resource = asm.GetManifestResourceNames().Single(n => n.EndsWith("golden.json", StringComparison.Ordinal));
        using Stream stream = asm.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())!;
    }
}
