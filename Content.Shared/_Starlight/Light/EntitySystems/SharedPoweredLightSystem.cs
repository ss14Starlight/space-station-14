// ReSharper disable once CheckNamespace
namespace Content.Shared.Light.EntitySystems;

public abstract partial class SharedPoweredLightSystem
{
    [SubscribeLocalEvent]
    private void OnGetDimmedLightLevel(EntityUid uid, AlertLevelDimmedLightComponent component, GetDimmedLightLevelEvent args)
    {
        if (!args.Dimmable)
            return;

        args.LightEnergy *= component.LightEnergyMultiplier;
        args.PowerUse *= component.LightEnergyMultiplier;
    }
}
