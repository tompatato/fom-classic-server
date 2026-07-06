namespace FOM.ClientChecks.Tests;

/// <summary>
/// Regression checks that the staged 2006 client still matches the reverse-
/// engineered facts our protocol/server are built on. If someone points
/// FOMC_GAME_DIR at a different build, these flag the divergence instead of us
/// silently shipping a server for the wrong binary.
/// </summary>
public class ReClientDataTests
{
    // The pristine CShell — prefer the .orig backup left by the localhost patch;
    // fall back to CShell.dll if the client was never patched.
    private static string CShellReference =>
        ClientBinaries.Path("Resources/CShell.dll.orig") is not null
            ? "Resources/CShell.dll.orig"
            : "Resources/CShell.dll";

    [ClientPresentFact]
    public void ExpectedModulesPresent()
    {
        string[] modules =
        [
            "FOM.exe", "Lithtech.exe",
            "Resources/CShell.dll", "Resources/Object.lto", "Resources/CRes.dll",
            "server.dll", "msvcr71.dll",
        ];
        foreach (string module in modules)
        {
            Assert.True(ClientBinaries.Path(module) is not null, $"missing expected module: {module}");
        }
    }

    [ClientPresentFact]
    public void NoRakNetSignatures()
    {
        string[] modules = ["Lithtech.exe", "Resources/CShell.dll", "Resources/Object.lto", "server.dll"];
        string[] signatures = ["RakNet", "RakPeer", "BitStream", "ID_CONNECTION_REQUEST"];
        foreach (string module in modules)
        {
            foreach (string sig in signatures)
            {
                Assert.False(ClientBinaries.ContainsAscii(module, sig),
                    $"unexpected RakNet signature '{sig}' in {module} — transport assumption (custom TCP/UDP) may be wrong");
            }
        }
    }

    [ClientPresentFact]
    public void UsesWinsockTransport()
    {
        Assert.True(ClientBinaries.ContainsAscii("Lithtech.exe", "WSOCK32"),
            "expected WSOCK32 import in Lithtech.exe");
        Assert.True(ClientBinaries.ContainsAscii("Lithtech.exe", "udp_BuildSockaddrFromString"),
            "expected the engine's UDP socket helper (confirms the UDP channel)");
    }

    [ClientPresentFact]
    public void ServerAddressTablePresent()
    {
        // The hardcoded server pool + its network-manager marker (see
        // knowledge-base/client/Network Address Table.md).
        Assert.True(ClientBinaries.ContainsAscii(CShellReference, "NETMGRCL"),
            $"missing NETMGRCL marker in {CShellReference}");
        Assert.True(ClientBinaries.ContainsAscii(CShellReference, "82.133.85.42"),
            "missing the original Duplex Systems server block");
        Assert.True(ClientBinaries.ContainsAscii(CShellReference, "82.133.85.52"),
            "missing the top of the original server block");
    }
}
