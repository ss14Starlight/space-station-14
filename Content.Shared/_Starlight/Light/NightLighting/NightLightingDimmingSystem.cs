namespace Content.Shared._Starlight.Light.NightLighting;

/// <summary>
/// Applies the night profile to lights carrying <see cref="NightLightingDimmedComponent"/>.
/// </summary>
public sealed partial class NightLightingDimmingSystem : EntitySystem
{
    /// <summary>
    /// Applies the night profile to brightness, radius, power use, and color, including bulbs exempt from alert dimming.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnGetDimmedLightLevel(EntityUid uid, NightLightingDimmedComponent comp, GetDimmedLightLevelEvent args)
    {
        args.LightEnergy *= comp.EnergyMultiplier;
        args.LightRadius *= comp.RadiusMultiplier;
        args.PowerUse *= comp.PowerMultiplier;
        args.Color *= comp.Tint;
    }
}
