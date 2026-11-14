using Content.Shared._Starlight.Light;
using Content.Shared._Starlight.ToggleableDevice.EntitySystems;
using Content.Shared.Light.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Light.EntitySystems;

public abstract partial class SharedPoweredLightSystem
{
    [Dependency] private ToggleableDeviceSystem _toggleableDevice = null!;

    [SubscribeLocalEvent]
    private void OnToggleablePreInit(Entity<PoweredLightComponent> ent, ref ToggleableDevicePreInitEvent ev)
        => ev.State = ent.Comp.On;

    [SubscribeLocalEvent]
    private void OnToggleableStateChanged(Entity<PoweredLightComponent> ent, ref ToggleableDeviceStateChangedEvent ev)
        => SetState(ent, ev.State, ent.Comp);

    private void SetToggleableState(EntityUid uid, bool state) =>
        // This might seem circular but the ToggleableDevice code checks for state equality.
        _toggleableDevice.SetState(uid, state);

    [SubscribeLocalEvent]
    private void OnGetDimmedLightLevel(EntityUid uid, AlertLevelDimmedLightComponent component, GetDimmedLightLevelEvent args)
    {
        if (!args.Dimmable)
            return;

        args.LightEnergy *= component.LightEnergyMultiplier;
        args.PowerUse *= component.LightEnergyMultiplier;
    }
}
