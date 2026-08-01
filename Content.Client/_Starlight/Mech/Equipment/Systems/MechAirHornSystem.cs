using Content.Shared._Starlight.Mech.Equipment.Components;
using Content.Shared._Starlight.Mech.Equipment.EntitySystems;
using Content.Shared.Mech;
using Robust.Shared.Audio.Systems;

namespace Content.Client._Starlight.Mech.Equipment.Systems;

public sealed partial class MechAirHornSystem : SharedMechAirHornSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    protected override void OnHonkHorn(EntityUid uid, MechAirHornComponent comp, MechActivateAirHornEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.Performer;
        var xform = Transform(user);
        _audio.PlayPredicted(comp.HornSound, xform.Coordinates, user);
    }
}
