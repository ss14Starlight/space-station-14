using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Toggle to allow all pipes to dock.
    /// </summary>
    public static readonly CVarDef<bool> DockPipes =
        CVarDef.Create("atmos.DockPipes", true, CVar.REPLICATED | CVar.SERVER);
}