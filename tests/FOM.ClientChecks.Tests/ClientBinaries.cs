using System.Text;

namespace FOM.ClientChecks.Tests;

/// <summary>
/// Locates and reads the staged 2006 client. Host-agnostic: the client lives
/// outside the repo and is never committed, so its directory is resolved from the
/// <c>FOMC_GAME_DIR</c> environment variable. When unset/missing, the RE checks
/// skip rather than fail (see <see cref="ClientPresentFactAttribute"/>).
/// </summary>
public static class ClientBinaries
{
    public static string? GameDir
    {
        get
        {
            string? dir = Environment.GetEnvironmentVariable("FOMC_GAME_DIR");
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir) ? dir : null;
        }
    }

    public static bool Available => GameDir is not null;

    /// <summary>Full path to a client file (relative to the game dir), or null if absent.</summary>
    public static string? Path(string relative)
    {
        if (GameDir is null)
        {
            return null;
        }
        string full = System.IO.Path.Combine(GameDir, relative);
        return File.Exists(full) ? full : null;
    }

    public static byte[] Read(string relative)
    {
        string? path = Path(relative) ?? throw new FileNotFoundException($"client file not found: {relative}");
        return File.ReadAllBytes(path);
    }

    /// <summary>True if the file's raw bytes contain the given ASCII string.</summary>
    public static bool ContainsAscii(string relative, string needle)
    {
        string? path = Path(relative);
        if (path is null)
        {
            return false;
        }
        ReadOnlySpan<byte> data = File.ReadAllBytes(path);
        return data.IndexOf(Encoding.ASCII.GetBytes(needle)) >= 0;
    }
}
