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
    /// Whether screens, lamps and other glowing sprites cast their colour around them through the bounce light.
    /// </summary>
    public static readonly CVarDef<bool> GlobalIlluminationEmission =
        CVarDef.Create("lighting.gi_emission", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether open space glows faintly through windows, giving the floor next to them a cold starlit tint.
    /// Walls still block it.
    /// </summary>
    public static readonly CVarDef<bool> GlobalIlluminationStarlight =
        CVarDef.Create("lighting.gi_starlight", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether bounce light takes the colour of the walls and floors it bounces off.
    /// A red carpet then gives the area around it a faint red glow.
    /// </summary>
    public static readonly CVarDef<bool> GlobalIlluminationSurfaceColor =
        CVarDef.Create("lighting.gi_surface_color", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Bounce-light resolution as a fraction of the light map's resolution, from 0.25 to 1.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationResolution =
        CVarDef.Create("lighting.gi_resolution", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Replaces the view with a debug view of the bounce passes. 0 is off, 1 shows the distance field, 2 shows only bounce light,
    /// 3 shows glowing pixels, 4 shows surface colours and 5 shows the station tile mask.
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

    /// <summary>
    /// How bright glowing sprites are as light sources for the bounce light, relative to the light a wall throws back.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationEmissionStrength =
        CVarDef.Create("lighting.gi_emission_strength", 1f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How much brighter than its light a pixel has to be to count as a light source, from 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationEmissionThreshold =
        CVarDef.Create("lighting.gi_emission_threshold", 0.1f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How bright open space is as a light source for the bounce light, on the same scale as
    /// <see cref="GlobalIlluminationEmissionStrength"/>.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationStarlightStrength =
        CVarDef.Create("lighting.gi_starlight_strength", 0.3f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How strongly a wall's own colour tints the light it throws back, from 0 to 1.
    /// The tint changes the colour of the bounce light but not its brightness.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationSurfaceTint =
        CVarDef.Create("lighting.gi_surface_tint", 0.6f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How strongly coloured floors tint the light around them, such as a red carpet giving off a red glow.
    /// It shifts colour without adding brightness, and grey floors do nothing. 0 turns it off.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationFloorBounce =
        CVarDef.Create("lighting.gi_floor_bounce", 10f, CVar.SERVER | CVar.REPLICATED);
}
