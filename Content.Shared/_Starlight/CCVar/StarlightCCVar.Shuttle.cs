using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    ///     How much time needs to pass before the arrivals shuttle can FTL again.
    ///     If this is adjusted, ensure that other Arrivals Cvars are adjusted
    ///     accordingly (like ArrivalsCooldown) in CCVars.Shuttle.cs.
    /// </summary>
    public static readonly CVarDef<float> ArrivalsFTLCooldown =
        CVarDef.Create("arrivals.cooldown", 10f, CVar.SERVERONLY);
}
