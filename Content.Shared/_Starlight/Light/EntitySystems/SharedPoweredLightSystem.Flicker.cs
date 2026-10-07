using Content.Shared._Starlight.Light.Flicker;
using Content.Shared.Light.Components;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Light.EntitySystems;

public abstract partial class SharedPoweredLightSystem
{
    /// <summary>
    /// Sets a flicker that replaces the bulb's own while the fixture is lit normally, null to clear it.
    /// </summary>
    public void SetFlickerOverride(Entity<LightFlickerComponent?> ent, ProtoId<LightFlickerPrototype>? flicker)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Override == flicker)
            return;

        ent.Comp.Override = flicker;
        Dirty(ent);
        UpdateLight(ent);
    }

    /// <summary>
    /// Whether the fixture is on, powered and has a working bulb.
    /// </summary>
    public bool IsLit(EntityUid uid, PoweredLightComponent? light = null)
        => Resolve(uid, ref light, false)
               && light.On
               && _receiver.IsPowered(uid)
               && GetBulb(uid, light) is { } bulb
               && TryComp<LightBulbComponent>(bulb, out var bulbComp)
               && bulbComp.State == LightBulbState.Normal;

    private void UpdateFlicker(EntityUid uid, EntityUid? bulb, bool lit, bool sparking)
    {
        if (!TryComp<LightFlickerComponent>(uid, out var flicker))
            return;

        TryComp<FlickeringBulbComponent>(bulb, out var flickering);

        ProtoId<LightFlickerPrototype>? profile = null;
        if (lit)
            profile = flicker.Override ?? flickering?.Flicker;
        else if (sparking)
            profile = flickering?.BrokenFlicker;

        if (flicker.Profile == profile)
            return;

        flicker.Profile = profile;
        Dirty(uid, flicker);
    }
}
