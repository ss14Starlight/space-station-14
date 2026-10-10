using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Self-explanatory
    /// </summary>
    public static readonly CVarDef<float> StationRadioVolume =
        CVarDef.Create("audio.station_radio_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Continuous station / maintenance / atmospherics hum (ambientLoop prototypes).
    /// </summary>
    public static readonly CVarDef<float> StationHumVolume =
        CVarDef.Create("audio.station_hum_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Random environment one-shots: hull creaks, pipe hisses (ambientOneShot prototypes).
    /// </summary>
    public static readonly CVarDef<float> EnvironmentVolume =
        CVarDef.Create("audio.environment_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How strongly rooms echo, scales every room reverb (roomReverb prototypes).
    /// </summary>
    public static readonly CVarDef<float> ReverbVolume =
        CVarDef.Create("audio.reverb_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// World sounds that belong to no other category.
    /// </summary>
    public static readonly CVarDef<float> EffectsVolume =
        CVarDef.Create("audio.effects_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Self-explanatory
    /// </summary>
    public static readonly CVarDef<float> FootstepsVolume =
        CVarDef.Create("audio.footsteps_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Picking up, dropping and equipping items.
    /// </summary>
    public static readonly CVarDef<float> HandlingVolume =
        CVarDef.Create("audio.handling_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Weapons and hits.
    /// </summary>
    public static readonly CVarDef<float> CombatVolume =
        CVarDef.Create("audio.combat_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Voices, emotes and creature noises (not TTS).
    /// </summary>
    public static readonly CVarDef<float> VoiceVolume =
        CVarDef.Create("audio.voice_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Station announcements and alert level sounds.
    /// </summary>
    public static readonly CVarDef<float> AnnouncementVolume =
        CVarDef.Create("audio.announcement_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
