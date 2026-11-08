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
    /// Whether the fixture is giving light from a working bulb on power. False for a fixture that is on
    /// but dimmed to nothing, like at alert levels that turn dimmable bulbs off, and for emergency light.
    /// </summary>
    public bool IsLit(Entity<PoweredLightComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        return ent.Comp.CurrentLit
            && _receiver.IsPowered(ent.Owner)
            && GetBulb(ent, ent.Comp) is { } bulb
            && TryComp<LightBulbComponent>(bulb, out var bulbComp)
            && bulbComp.State == LightBulbState.Normal;
    }

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
