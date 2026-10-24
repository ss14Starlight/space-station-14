using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<string> AdminGhostScriptPath =
        CVarDef.Create("admin.admin_ghost_script_path", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether this client shows tippy at all, admin-sent or not.
    /// </summary>
    public static readonly CVarDef<bool> TippyClientEnabled =
        CVarDef.Create("tips.client_tippy_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);


    /// <summary>
    /// Seconds between two tippies sent by admins, shared by all admins.
    /// </summary>
    public static readonly CVarDef<float> AdminTippyCooldown =
        CVarDef.Create("admin.tippy_cooldown", 10f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Longest time a tippy may stay on screen, in seconds. Applies to every tippy, not only admin ones.
    /// </summary>
    public static readonly CVarDef<float> TippyMaxSpeakTime =
        CVarDef.Create("admin.tippy_max_speak_time", 30f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Longest tippy message, in characters. Longer ones are cut.
    /// </summary>
    public static readonly CVarDef<int> TippyMaxLength =
        CVarDef.Create("admin.tippy_max_length", 400, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Seconds between two admin global sounds, shared by all admins.
    /// </summary>
    public static readonly CVarDef<float> AdminGlobalSoundCooldown =
        CVarDef.Create("admin.global_sound_cooldown", 5f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Loudest volume, in dB, an admin global sound may be played at.
    /// </summary>
    public static readonly CVarDef<float> AdminGlobalSoundMaxVolume =
        CVarDef.Create("admin.global_sound_max_volume", 0f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Largest total intensity of an admin-spawned explosion. A station nuke is 5 000 000.
    /// </summary>
    public static readonly CVarDef<float> AdminExplosionMaxIntensity =
        CVarDef.Create("admin.explosion_max_intensity", 50000f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Largest per-tile intensity of an admin-spawned explosion.
    /// </summary>
    public static readonly CVarDef<float> AdminExplosionMaxTileIntensity =
        CVarDef.Create("admin.explosion_max_tile_intensity", 200f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Seconds between two explosions spawned by the same admin.
    /// </summary>
    public static readonly CVarDef<float> AdminExplosionCooldown =
        CVarDef.Create("admin.explosion_cooldown", 2f, CVar.SERVERONLY | CVar.ARCHIVE);
}
