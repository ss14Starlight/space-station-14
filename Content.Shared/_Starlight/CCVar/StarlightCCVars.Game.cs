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
    /// Sends afk players to cryo.
    /// </summary>
    public static readonly CVarDef<float> AdmemeShuttleLimit =
        CVarDef.Create("game.admeme_shuttle_limit", 1000f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether the `mapping` command is disabled cause it fucks with the event scheduler and admins just cant stop touching it.
    /// </summary>
    public static readonly CVarDef<bool> FuckMappingCommand =
        CVarDef.Create("game.fuck_mapping", false, CVar.SERVERONLY | CVar.CONFIDENTIAL);

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
}
