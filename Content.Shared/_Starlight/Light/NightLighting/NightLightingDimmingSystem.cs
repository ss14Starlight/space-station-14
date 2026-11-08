namespace Content.Shared._Starlight.Light.NightLighting;

/// <summary>
/// Applies the night profile to lights carrying <see cref="NightLightingDimmedComponent"/>.
/// </summary>
public sealed partial class NightLightingDimmingSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnGetDimmedLightLevel(EntityUid uid, NightLightingDimmedComponent comp, GetDimmedLightLevelEvent args)
    {
        args.LightEnergy *= comp.EnergyMultiplier;
        args.LightRadius *= comp.RadiusMultiplier;
        args.PowerUse *= comp.PowerMultiplier;
        args.Color *= comp.Tint;
    }
}
