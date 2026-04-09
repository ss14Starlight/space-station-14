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
        if (!TryComp<SlipperyComponent>(uid, out var slipComp))
            return;
        var user = args.Performer;
        slipComp.SlipData.RequiredSlipSpeed = 0f;
        slipComp.SlipData.KnockdownTime = TimeSpan.FromSeconds(.8);
        slipComp.SlipData.StunTime = TimeSpan.FromSeconds(.2);
        var xform = Transform(user);
        foreach (var ent in _entityLookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, 10.0f, LookupFlags.Uncontained))
        {
            _slippery.TrySlip(args.Performer, slipComp, ent, false);
        }
    }
}
