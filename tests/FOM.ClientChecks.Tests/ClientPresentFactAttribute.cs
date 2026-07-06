namespace FOM.ClientChecks.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that skips (rather than fails) when the 2006
/// client isn't available, so these binary-facing checks never break a clean
/// checkout or CI. Set <c>FOMC_GAME_DIR</c> to the staged client to run them.
/// </summary>
public sealed class ClientPresentFactAttribute : FactAttribute
{
    public ClientPresentFactAttribute()
    {
        if (!ClientBinaries.Available)
        {
            Skip = "2006 client not present; set FOMC_GAME_DIR to the staged client to run RE checks";
        }
    }
}
