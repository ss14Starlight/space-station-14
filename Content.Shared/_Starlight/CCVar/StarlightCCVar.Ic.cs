using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Restricts IC character custom specie names so they cannot be others species.
    /// </summary>
    public static readonly CVarDef<bool> RestrictedCustomSpecieNames =
        CVarDef.Create("ic.restricted_customspecienames", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Allows IC Secrets (flavor text only visible to the player possessing the character)
    /// </summary>
    public static readonly CVarDef<bool> ICSecrets =
        CVarDef.Create("ic.secrets_text", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Allows IC Exploitables (flavor text only visible to the player possessing the player and certain antags)
    /// </summary>
    public static readonly CVarDef<bool> ExploitableSecrets =
        CVarDef.Create("ic.secrets_exploitable", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether or not players can open character inspect windows on other players
    /// </summary>
    public static readonly CVarDef<bool> CharacterInspectWindowEnabled =
        CVarDef.Create("ic.inspect_windows", false, CVar.SERVER | CVar.REPLICATED);

    /*
     * Traits
     */

    /// <summary>
    /// Maximum number of traits that can be selected globally.
    /// </summary>
    public static readonly CVarDef<int> MaxTraitCount =
        CVarDef.Create("ic.traits.max_count", 10, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Maximum trait points available to spend.
    /// Traits with positive cost consume points, negative cost traits grant points.
    /// </summary>
    public static readonly CVarDef<int> MaxTraitPoints =
        CVarDef.Create("ic.traits.max_points", 15, CVar.SERVER | CVar.REPLICATED);
}
