using Content.Shared.Mech;
using Content.Shared.Mobs.Components;
using Content.Shared.Slippery;
using Content.Shared._Starlight.Mech.Equipment.Components;
using Content.Shared._Starlight.Mech.Equipment.EntitySystems;

namespace Content.Server._Starlight.Mech.Equipment.EntitySystems;

public sealed class MechAirHornSystem : SharedMechAirHornSystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SlipperySystem _slippery = default!;

    protected override void OnHonkHorn(EntityUid uid, MechAirHornComponent comp, MechActivateAirHornEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<SlipperyComponent>(uid, out var slipComp))
            return;

        args.Handled = true;

        var user = args.Performer;
        var xform = Transform(user);
        foreach (var ent in _entityLookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, comp.Range, LookupFlags.Uncontained))
        {
            _slippery.TrySlip(args.Performer, slipComp, ent, false);
        }
    }
}
