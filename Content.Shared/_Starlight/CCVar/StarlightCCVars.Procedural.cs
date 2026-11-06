using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Seconds per tick dungeon generation (expeditions, salvage magnet, VGRoid, ...) is allowed to use.
    /// Lower values spread generation over more ticks: less tick time, slower generation.
    /// </summary>
    public static readonly CVarDef<double> DungeonJobTime =
        CVarDef.Create("procgen.dungeon_job_time", 0.0025, CVar.SERVERONLY);
}
