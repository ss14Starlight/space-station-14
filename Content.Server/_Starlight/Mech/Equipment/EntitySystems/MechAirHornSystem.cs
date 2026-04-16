using Content.Shared.Mech;
using Content.Shared.Mobs.Components;
using Content.Shared.Slippery;
using Content.Shared._Starlight.Mech.Equipment.Components;
using Content.Shared._Starlight.Mech.Equipment.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Mech.Equipment.EntitySystems;

public sealed class MechAirHornSystem : SharedMechAirHornSystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SlipperySystem _slippery = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    protected override void OnHonkHorn(EntityUid uid, MechAirHornComponent comp, MechActivateAirHornEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<SlipperyComponent>(uid, out var slipComp))
            return;

        args.Handled = true;

        var user = args.Performer;
        var xform = Transform(user);
        _audio.PlayPredicted(comp.HornSound, xform.Coordinates, user);

        foreach (var ent in _entityLookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, comp.Range, LookupFlags.Uncontained))
        {
            _slippery.TrySlip(uid, slipComp, ent, false);
        }
    }
}
