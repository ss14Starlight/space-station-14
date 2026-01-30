using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// THe ammount of new lifes a player can have in a round.
    /// </summary>
    public static readonly CVarDef<int> MaxNewLifes =
        CVarDef.Create("newlife.max_new_lifes", 5, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE,
            "The maximum number of new lifes a player can have in a round.");
    
    public static readonly CVarDef<int> NewLifeGhostCooldown =
        CVarDef.Create("newlife.ghost_cooldown", (int)TimeSpan.FromMinutes(5).TotalSeconds, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE,
            "The time to wait after ghosting before being able to take a new life, in seconds");
}
