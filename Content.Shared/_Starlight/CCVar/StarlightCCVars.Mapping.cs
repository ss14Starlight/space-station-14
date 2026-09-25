using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<int> MaxAutoAtmosLinkDistance =
        CVarDef.Create("mapping.atmos_autolink_dist", 10, CVar.SERVER | CVar.ARCHIVE);

}
