using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Whether light from illuminated walls bounces onto the surrounding floor. Uses a few GPU passes per frame.
    /// </summary>
    public static readonly CVarDef<bool> GlobalIllumination =
        CVarDef.Create("lighting.gi", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Number of rays cast by each pixel, from 4 to 32. More rays reduce noise.
    /// </summary>
    public static readonly CVarDef<int> GlobalIlluminationRays =
        CVarDef.Create("lighting.gi_rays", 12, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether bounce light is accumulated across frames. This greatly reduces noise and allows several bounces instead of one,
    /// but causes light trails to change over a few frames.
    /// </summary>
    public static readonly CVarDef<bool> GlobalIlluminationTemporal =
        CVarDef.Create("lighting.gi_temporal", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Bounce-light resolution as a fraction of the light map's resolution, from 0.25 to 1.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationResolution =
        CVarDef.Create("lighting.gi_resolution", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Replaces the light map with a view of the bounce passes: 0 disables the view, 1 shows the distance field, and 2 shows only bounce light.
    /// </summary>
    public static readonly CVarDef<int> GlobalIlluminationDebug =
        CVarDef.Create("lighting.gi_debug", 0, CVar.CLIENTONLY);

    /// <summary>
    /// The amount of light a wall reflects back onto the floor.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationStrength =
        CVarDef.Create("lighting.gi_strength", 0.6f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The distance, in tiles, that bounced light travels from the wall reflecting it.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationDistance =
        CVarDef.Create("lighting.gi_distance", 6f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The amount of blur applied to bounce light to reduce ray noise, measured in the same units as the engine's light blur.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationBlur =
        CVarDef.Create("lighting.gi_blur", 4f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
