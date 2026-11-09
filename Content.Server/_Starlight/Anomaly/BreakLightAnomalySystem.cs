using Content.Shared.Anomaly.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Light.Components;
using Content.Server._Starlight.Anomaly.Components;

namespace Content.Server._Starlight.Anomaly;

public sealed partial class BreakLightAnomalySystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPoweredLightSystem _light = default!;

    public override void Initialize() => base.Initialize();

    [SubscribeLocalEvent]
    private void OnPulse(Entity<BreakLightAnomalyComponent> ent, ref AnomalyPulseEvent args)
    {
        var range = ent.Comp.PulseRange * args.Stability * args.PowerModifier;

        var xform = Transform(ent);
        foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(xform.Coordinates, range))
            _light.TryDestroyBulb(light, light.Comp);
    }

    [SubscribeLocalEvent]
    private void OnSupercritical(Entity<BreakLightAnomalyComponent> ent, ref AnomalySupercriticalEvent args)
    {
        var range = ent.Comp.PulseRange * 3 * args.PowerModifier;

        var xform = Transform(ent);
        foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(xform.Coordinates, range))
            _light.TryDestroyBulb(light.Owner, light.Comp);
    }
}
