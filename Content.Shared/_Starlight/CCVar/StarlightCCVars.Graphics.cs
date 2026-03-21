using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> HolesEnabled =
        CVarDef.Create("opt.holes_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> TracesEnabled =
        CVarDef.Create("opt.traces_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> SparksEnabled =
        CVarDef.Create("opt.sparks_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
