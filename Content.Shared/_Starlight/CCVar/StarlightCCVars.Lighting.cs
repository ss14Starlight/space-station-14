using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Whether the station dims its lights for the night shift on its own.
    /// APCs still switch to night lighting on low power and by hand when this is off.
    /// </summary>
    public static readonly CVarDef<bool> NightShiftEnabled =
        CVarDef.Create("nightshift.enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Station time, in hours, at which the night shift starts.
    /// </summary>
    public static readonly CVarDef<float> NightShiftStartHour =
        CVarDef.Create("nightshift.start_hour", 19.5f, CVar.SERVERONLY);

    /// <summary>
    /// Station time, in hours, at which the night shift ends.
    /// </summary>
    public static readonly CVarDef<float> NightShiftEndHour =
        CVarDef.Create("nightshift.end_hour", 7.5f, CVar.SERVERONLY);

    /// <summary>
    /// Whether the station announces the start and the end of the night shift.
    /// </summary>
    public static readonly CVarDef<bool> NightShiftAnnounce =
        CVarDef.Create("nightshift.announce", true, CVar.SERVERONLY);

    /// <summary>
    /// Whether APCs in auto mode switch to night lighting while their battery drains.
    /// </summary>
    public static readonly CVarDef<bool> NightShiftLowPower =
        CVarDef.Create("nightshift.low_power", true, CVar.SERVERONLY);

    /// <summary>
    /// Earliest station time, in hours, a shift can start at.
    /// </summary>
    public static readonly CVarDef<int> ShiftStartMinHour =
        CVarDef.Create("time.shift_start_min_hour", 8, CVar.SERVERONLY);

    /// <summary>
    /// Latest station time, in hours, a shift can start at. Set both bounds to 12 for the old fixed noon start.
    /// </summary>
    public static readonly CVarDef<int> ShiftStartMaxHour =
        CVarDef.Create("time.shift_start_max_hour", 22, CVar.SERVERONLY);

    /// <summary>
    /// Whether the client rolls off the light map where lights overlap, instead of washing sprites out.
    /// </summary>
    public static readonly CVarDef<bool> LightTonemap =
        CVarDef.Create("lighting.tonemap", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// The light map is multiplied by this before the roll-off.
    /// </summary>
    public static readonly CVarDef<float> LightTonemapExposure =
        CVarDef.Create("lighting.tonemap_exposure", 1f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Light level the roll-off starts at, everything below it is left as is.
    /// Has to be below <see cref="LightTonemapMax"/>, the client lowers it to just under the maximum otherwise.
    /// </summary>
    public static readonly CVarDef<float> LightTonemapKnee =
        CVarDef.Create("lighting.tonemap_knee", 0.7f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Light level the roll-off approaches but never reaches, however many lights overlap.
    /// Has to be above <see cref="LightTonemapKnee"/>.
    /// </summary>
    public static readonly CVarDef<float> LightTonemapMax =
        CVarDef.Create("lighting.tonemap_max", 1.15f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether poorly lit areas lose their colour and turn blue, the way the eye sees in the dark.
    /// </summary>
    public static readonly CVarDef<bool> NightVision =
        CVarDef.Create("lighting.night_vision", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How far night vision goes in the dark, from 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> NightVisionStrength =
        CVarDef.Create("lighting.night_vision_strength", 0.7f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether screens, lamps and other glowing things bleed light around them. Costs a few extra passes per frame.
    /// </summary>
    public static readonly CVarDef<bool> Bloom =
        CVarDef.Create("lighting.bloom", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How bright the bloom is, from 0 to 3. The blur spreads the glow thin, so values above 1 are useful.
    /// </summary>
    public static readonly CVarDef<float> BloomStrength =
        CVarDef.Create("lighting.bloom_strength", 1.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How much a pixel has to outshine its light before it glows, from 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> BloomThreshold =
        CVarDef.Create("lighting.bloom_threshold", 0.15f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How far the bloom spreads. Same units as the engine's light blur.
    /// </summary>
    public static readonly CVarDef<float> BloomRadius =
        CVarDef.Create("lighting.bloom_radius", 10f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the edges of the view are darkened.
    /// </summary>
    public static readonly CVarDef<bool> Vignette =
        CVarDef.Create("lighting.vignette", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How much the corners of the view are darkened, from 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> VignetteStrength =
        CVarDef.Create("lighting.vignette_strength", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether film grain is drawn over the view.
    /// </summary>
    public static readonly CVarDef<bool> FilmGrain =
        CVarDef.Create("lighting.film_grain", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Strength of the film grain, as a fraction of full brightness.
    /// </summary>
    public static readonly CVarDef<float> FilmGrainStrength =
        CVarDef.Create("lighting.film_grain_strength", 0.05f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
