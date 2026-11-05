using Content.Shared._Starlight.Light.EmergencyLighting;
using Content.Shared._Starlight.Light.NightLighting;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Light.Components;
using Content.Shared.Power.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Light.EntitySystems;

public abstract partial class SharedPoweredLightSystem
{
    private bool TrySetEmergencyLight(EntityUid uid,
        PoweredLightComponent light,
        Entity<LightBulbComponent> bulb,
        SharedApcPowerReceiverComponent powerReceiver,
        AppearanceComponent? appearance)
    {
        if (!TryComp<EmergencyLightingComponent>(uid, out var emergency))
            return false;

        if (powerReceiver.Powered || !light.On || !emergency.Charged)
        {
            _appearance.SetData(uid, EmergencyLightingVisuals.Active, false, appearance);
            return false;
        }

        var radius = bulb.Comp.LightRadius * emergency.RadiusMultiplier;
        var energy = bulb.Comp.LightEnergy * emergency.EnergyMultiplier;

        if (TryComp<NightLightingDimmedComponent>(uid, out var night))
        {
            radius *= night.RadiusMultiplier;
            energy *= night.EnergyMultiplier;
        }

        SetLight(uid,
            true,
            emergency.Color,
            light,
            radius,
            energy,
            bulb.Comp.LightSoftness,
            HasComp<DarkLightComponent>(bulb));

        _appearance.SetData(uid, PoweredLightVisuals.BulbState, PoweredLightState.On, appearance);
        // Also gets the visualizer to pick up the new glow colour, the bulb state alone may not have changed.
        _appearance.SetData(uid, EmergencyLightingVisuals.Active, true, appearance);
        return true;
    }

    private void ClearEmergencyLight(EntityUid uid, AppearanceComponent? appearance)
    {
        if (HasComp<EmergencyLightingComponent>(uid))
            _appearance.SetData(uid, EmergencyLightingVisuals.Active, false, appearance);
    }
}
