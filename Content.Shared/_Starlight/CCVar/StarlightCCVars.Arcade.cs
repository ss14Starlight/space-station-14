using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    ///     The fallback maximum number of high score entries for arcade scoreboards
    ///     that do not have a max entry count defined.
    /// </summary>
    public static readonly CVarDef<int> FallbackScoreboardEntriesCount =
        CVarDef.Create("arcade.fallback_scoreboard_entries", 5, CVar.SERVERONLY);
}
