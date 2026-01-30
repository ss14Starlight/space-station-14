using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> DockHV =
        CVarDef.Create("power.DockHV", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockMV =
        CVarDef.Create("power.DockMV", false, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockLV =
        CVarDef.Create("power.DockLV", false, CVar.REPLICATED | CVar.SERVER);
}