using System.Linq;
using Content.Server._Moffstation.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Objectives.Systems;

public sealed class PickRoomObjectiveSystem : EntitySystem
{
    [Dependency] private readonly LocationObjectiveSystem _target = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickRoomObjectiveComponent, ObjectiveAssignedEvent>(OnRandomRoomAssigned);

    }

    private void OnRandomRoomAssigned(Entity<PickRoomObjectiveComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<LocationObjectiveComponent>(ent.Owner, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;


        var beacons = new List<NavMapBeaconComponent>();
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var transform))
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
