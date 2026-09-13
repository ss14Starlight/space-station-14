using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<int> MaxZoneTilesPerTick =
        CVarDef.Create("zones.max_tiles_per_tick", 32, CVar.SERVERONLY);

    public static readonly CVarDef<int> MaxZoneRenamesPerTick =
        CVarDef.Create("zones.max_renames_per_tick", 2, CVar.SERVERONLY);

    public static readonly CVarDef<int> MaxZoneSearchVisits =
        CVarDef.Create("zones.max_search_visits", 6144, CVar.SERVERONLY);

    public static readonly CVarDef<int> MaxZoneSeeds =
        CVarDef.Create("zones.max_seeds", 5, CVar.SERVERONLY);
}
