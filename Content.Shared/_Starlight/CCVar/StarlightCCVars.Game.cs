using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Making everyone a pacifist at the end of a round.
    /// </summary>
    public static readonly CVarDef<bool> PeacefulRoundEnd =
        CVarDef.Create("game.peaceful_end", true, CVar.SERVERONLY);

    /// <summary>
    /// Sends afk players to cryo.
    /// </summary>
    public static readonly CVarDef<bool> CryoTeleportation =
        CVarDef.Create("game.cryo_teleportation", true, CVar.SERVERONLY);

    /// <summary>
    /// A limit on the maximum manual FTL range for shuttles, even if the shuttle's components are modified.
    /// </summary>
    public static readonly CVarDef<float> AdmemeShuttleLimit =
        CVarDef.Create("game.admeme_shuttle_limit", 1000f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether the `mapping` command is disabled cause it fucks with the event scheduler and admins just cant stop touching it.
    /// </summary>
    public static readonly CVarDef<bool> FuckMappingCommand =
        CVarDef.Create("game.fuck_mapping", false, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// A multiplier for how much everyone gets paid on salary ticks, e.g. for hazard pay to encourage playing on test branches.
    /// </summary>
    public static readonly CVarDef<float> SalaryMultiplier =
        CVarDef.Create("game.salary_multiplier", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Amount of required fully charged rifts for automatic gamma calling
    /// </summary>
    public static readonly CVarDef<int> AutogammaRiftCount =
        CVarDef.Create("game.autogamma_minimum_rifts", 2, CVar.SERVERONLY);

    /// <summary>
    /// Whether gamma will automatically be called upon <see cref="AutogammaRiftCount"/> fully charged rifts
    /// </summary>
    public static readonly CVarDef<bool> AutogammaRiftEnabled =
        CVarDef.Create("game.autogamma_enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// The time in seconds that it will take the round to end after the nuke explodes durign nukies.
    /// </summary>
    public static readonly CVarDef<float> NukeRoundRestartTime =
        CVarDef.Create("game.nuke_round_restart_time", 20f, CVar.SERVERONLY);
}
