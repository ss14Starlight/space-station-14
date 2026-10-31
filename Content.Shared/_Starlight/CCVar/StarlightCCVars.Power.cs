using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> DockHV =
        CVarDef.Create("power.DockHV", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockMV =
        CVarDef.Create("power.DockMV", false, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockLV =
        CVarDef.Create("power.DockLV", false, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Run the power net solver once every N ticks, feeding it the accumulated frame time.
    /// 1 = every tick (upstream behaviour). Power changes become visible up to N-1 ticks later.
    /// </summary>
    public static readonly CVarDef<int> PowerNetTickInterval =
        CVarDef.Create("power.net_tick_interval", 1, CVar.SERVERONLY);
}
