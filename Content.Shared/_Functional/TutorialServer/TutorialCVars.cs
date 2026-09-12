using Robust.Shared.Configuration;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Functional Tutorial Server CVars. Defaults leave production behavior unchanged.
/// </summary>
[CVarDefs]
public sealed class TutorialCVars
{
    /// <summary>
    /// When true, the client automatically readies in the lobby (for e2e smoke tests).
    /// </summary>
    public static readonly CVarDef<bool> E2EAutoReady =
        CVarDef.Create("tutorial.e2e_auto_ready", false, CVar.CLIENTONLY);

    /// <summary>
    /// When set (e.g. TutorialPassenger), skip the role picker and start that tutorialRole on spawn.
    /// </summary>
    public static readonly CVarDef<string> E2EAutoRole =
        CVarDef.Create("tutorial.e2e_auto_role", "", CVar.SERVERONLY);

    /// <summary>
    /// When true, force-start the round shortly after the first player connects (lobby).
    /// </summary>
    public static readonly CVarDef<bool> E2EForceStart =
        CVarDef.Create("tutorial.e2e_force_start", false, CVar.SERVERONLY);

    /// <summary>
    /// When true, the role picker lists only tutorials marked <c>liveTutorial</c>.
    /// Enabled on the deployed Tutorial host; left off in development so unfinished
    /// tutorials stay visible (shown with a stub prefix).
    /// </summary>
    public static readonly CVarDef<bool> LiveTutorials =
        CVarDef.Create("tutorial.live_tutorials", false, CVar.SERVERONLY);

    /// <summary>
    /// When false, the ghost-roles button is hidden and takeover requests are rejected.
    /// TutorialServer turns this off for the duration of the rule.
    /// </summary>
    public static readonly CVarDef<bool> GhostRolesEnabled =
        CVarDef.Create("tutorial.ghost_roles_enabled", true, CVar.REPLICATED | CVar.SERVER);
}
