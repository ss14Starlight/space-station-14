using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Whether light bounces off lit walls onto the floor around them. Costs a few GPU passes per frame.
    /// </summary>
    public static readonly CVarDef<bool> GlobalIllumination =
        CVarDef.Create("lighting.gi", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How many rays every pixel casts, from 4 to 32. More rays mean less noise.
    /// </summary>
    public static readonly CVarDef<int> GlobalIlluminationRays =
        CVarDef.Create("lighting.gi_rays", 12, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether bounce light is accumulated over frames: much less noise and several bounces instead of one,
    /// but the light trails changes by a few frames.
    /// </summary>
    public static readonly CVarDef<bool> GlobalIlluminationTemporal =
        CVarDef.Create("lighting.gi_temporal", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Resolution of the bounce light, as a fraction of the light map's, from 0.25 to 1.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationResolution =
        CVarDef.Create("lighting.gi_resolution", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Replaces the light map with a view of the bounce passes: 0 off, 1 distance field, 2 bounce light only.
    /// </summary>
    public static readonly CVarDef<int> GlobalIlluminationDebug =
        CVarDef.Create("lighting.gi_debug", 0, CVar.CLIENTONLY);

    /// <summary>
    /// How much of the light a wall gets it throws back onto the floor.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationStrength =
        CVarDef.Create("lighting.gi_strength", 0.6f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How far, in tiles, bounced light reaches from the wall that throws it.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationDistance =
        CVarDef.Create("lighting.gi_distance", 6f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How far the bounce light is blurred to hide the ray noise. Same units as the engine's light blur.
    /// </summary>
    public static readonly CVarDef<float> GlobalIlluminationBlur =
        CVarDef.Create("lighting.gi_blur", 4f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
