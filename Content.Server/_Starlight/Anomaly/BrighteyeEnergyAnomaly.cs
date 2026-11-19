using Content.Shared.Anomaly.Components;
using Robust.Shared.Random;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Server._Starlight.Anomaly.Components;

namespace Content.Server._Starlight.Anomaly;

public sealed partial class BrighteyeEnergyAnomaly : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize() => base.Initialize();

    [SubscribeLocalEvent]
    private void OnPulse(Entity<BrighteyeEnergyAnomalyComponent> ent, ref AnomalyPulseEvent args)
    {
        var range = ent.Comp.PulseRange * args.Stability * args.PowerModifier;
        var xform = Transform(ent);
        var newEnergy = _random.Next(5, 30) * (int)args.Stability * (int)args.PowerModifier;

        foreach (var brighteye in _lookup.GetEntitiesInRange<BrighteyeComponent>(xform.Coordinates, range))
        {
            brighteye.Comp.Energy = Math.Clamp(brighteye.Comp.Energy + newEnergy, 0, brighteye.Comp.MaxEnergy);
            Dirty(brighteye);
        }
    }

    [SubscribeLocalEvent]
    private void OnSupercritical(Entity<BrighteyeEnergyAnomalyComponent> ent, ref AnomalySupercriticalEvent args)
    {
        var range = ent.Comp.PulseRange * 3 * args.PowerModifier;
        var xform = Transform(ent);

        foreach (var brighteye in _lookup.GetEntitiesInRange<BrighteyeComponent>(xform.Coordinates, range))
        {
            brighteye.Comp.Energy = brighteye.Comp.MaxEnergy;
            Dirty(brighteye);
        }

        // TODO: Add a Chance that a Brighteye might spawn on SuperCrit (Will respect Gamerules limits)
    }
}
