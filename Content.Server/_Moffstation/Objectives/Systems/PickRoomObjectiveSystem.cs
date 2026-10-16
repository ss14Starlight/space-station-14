using Content.Server._Moffstation.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Pinpointer;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Objectives.Systems;

public sealed partial class PickRoomObjectiveSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickRoomObjectiveComponent, ObjectiveAssignedEvent>(OnRandomRoomAssigned);
    }

    private void OnRandomRoomAssigned(Entity<PickRoomObjectiveComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // Invalid objective prototype.
        if (!TryComp<LocationObjectiveComponent>(ent.Owner, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // Target already assigned.
        if (target.Target != null)
            return;
        var beacons = new List<NavMapBeaconComponent>();
        var query = EntityQueryEnumerator<NavMapBeaconComponent>(); // Starlight
        while (query.MoveNext(out _, out var beacon)) // Starlight
        {
            if (beacon.Text == null)
                continue;
            if (ent.Comp.RoomBlacklist.Contains(beacon.Text))
                continue;
            if (ent.Comp.RoomWhitelist.Count != 0 && !ent.Comp.RoomWhitelist.Contains(beacon.Text))
                continue;

            beacons.Add(beacon);
        }

        if (beacons.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        target.Target = _random.Pick(beacons).Text;
    }
}
