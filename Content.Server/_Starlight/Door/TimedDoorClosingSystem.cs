using Content.Server.Doors.Systems;
using Content.Shared._Starlight.Door;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Door;

public sealed class TimedDoorClosingSystem : EntitySystem
{
    [Dependency] private DoorSystem _door = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<Entity<PhysicsComponent>> _intersecting = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedDoorClosingComponent, DoorStateChangedEvent>(OnDoorStateChanged);
    }

    private void OnDoorStateChanged(Entity<TimedDoorClosingComponent> ent, ref DoorStateChangedEvent args)
    {
        if (args.State != DoorState.Open)
            return;

        ent.Comp.NextClose = _timing.CurTime + ent.Comp.AutoCloseDelay;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TimedDoorClosingComponent, DoorComponent>();

        while (query.MoveNext(out var uid, out var timed, out var door))
        {
            if (timed.NextClose is not { } nextClose || nextClose > _timing.CurTime)
                continue;

            timed.NextClose = null;

            if (door.State != DoorState.Open)
                continue;

            // Don't close while something is in the doorway.
            if (IsBlocked(uid))
            {
                timed.NextClose = _timing.CurTime + timed.AutoCloseDelay;
                continue;
            }

            _door.TryClose(uid, door);
        }
    }

    private bool IsBlocked(EntityUid uid)
    {
        var xform = Transform(uid);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tileRef = _mapSystem.GetTileRef(gridUid, grid, xform.Coordinates);

        _intersecting.Clear();

        _entityLookup.GetLocalEntitiesIntersecting(
            gridUid,
            tileRef.GridIndices,
            _intersecting,
            gridComp: grid,
            flags: LookupFlags.All & ~LookupFlags.Sensors);

        foreach (var entity in _intersecting)
        {
            if (entity.Owner == uid)
                continue;

            if (!entity.Comp.CanCollide)
                continue;

            return true;
        }

        return false;
    }
}
