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
    /// Restrict (sanitize) nicknames.
    /// </summary>
    public static readonly CVarDef<bool> RestrictedNicknames =
        CVarDef.Create("ic.restricted_nicknames", true, CVar.SERVER | CVar.REPLICATED);

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
    /// Minimum nickname length (0 = no minimum).
    /// </summary>
    public static readonly CVarDef<int> MinNicknameLength =
        CVarDef.Create("ic.nickname_min_length", 2, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether or not players can open character inspect windows on other players
    /// </summary>
    public static readonly CVarDef<bool> CharacterInspectWindowEnabled =
        CVarDef.Create("ic.inspect_windows", false, CVar.SERVER | CVar.REPLICATED);
}