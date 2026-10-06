using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> HitscanPrediction =
        CVarDef.Create("opt.hitscan_prediction", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    [CVarControl(AdminFlags.VarEdit)]
    public static readonly CVarDef<bool> HitscanLagCompensation =
        CVarDef.Create("weapons.hitscan_lag_compensation", true, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> HitscanLagCompensationMaxMs =
        CVarDef.Create("weapons.hitscan_lag_compensation_max_ms", 400, CVar.SERVERONLY);

    public static readonly CVarDef<int> HitscanLagCompensationExtraTicks =
        CVarDef.Create("weapons.hitscan_lag_compensation_extra_ticks", 2, CVar.SERVERONLY);
}
