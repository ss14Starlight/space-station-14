using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Put HTN NPCs to sleep when no player is near them, and wake them back up once one comes close.
    /// </summary>
    public static readonly CVarDef<bool> NPCProximitySleep =
        CVarDef.Create("npc.proximity_sleep", true, CVar.SERVERONLY);

    /// <summary>
    /// Distance in tiles from the nearest player beyond which an NPC is put to sleep.
    /// Keep it above the largest NPC perception or weapon range so mobs are awake before anyone sees them.
    /// </summary>
    public static readonly CVarDef<float> NPCProximitySleepRange =
        CVarDef.Create("npc.proximity_sleep_range", 32f, CVar.SERVERONLY);

    /// <summary>
    /// How often in seconds NPC proximity is re-evaluated.
    /// </summary>
    public static readonly CVarDef<float> NPCProximitySleepInterval =
        CVarDef.Create("npc.proximity_sleep_interval", 1f, CVar.SERVERONLY);
}
