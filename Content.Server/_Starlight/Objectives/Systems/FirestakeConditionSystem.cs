using System.Linq;
using Content.Server.Buckle.Systems;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Server._Starlight.Objectives.Components;
using Content.Server._Starlight.Roles;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Antags.BloodBound.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed partial class FirestakeConditionSystem : EntitySystem
{
    [Dependency] private ZoneSystem _zones = default!;
    [Dependency] private StationSystem _stations = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private NumberObjectiveSystem _numbers = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(IgniteOnBuckleSystem));
        SubscribeLocalEvent<FirestakeConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<FirestakeConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<FirestakeConditionComponent, ObjectiveGetProgressEvent>(OnProgress);
    }

    private void OnAssigned(Entity<FirestakeConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Mind.OwnedEntity is not { } body)
        {
            args.Cancelled = true;
            return;
        }

        var station = _stations.GetOwningStation(body);
        var grid = station is { } stationUid ? _stations.GetLargestGrid(stationUid) : Transform(body).GridUid;
        if (grid is not { } gridUid || _zones.GetGridZones(gridUid).ToList() is not { Count: > 0 } zones)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.AssignedMind = args.MindId;
        ent.Comp.Grid = gridUid;
        ent.Comp.Zone = _random.Pick(zones);
    }

    private void OnAfterAssign(Entity<FirestakeConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (ent.Comp.Zone is not { } zone)
            return;

        var name = Loc.GetString(_prototypes.Index(zone).Name);
        var count = _numbers.GetTarget(ent);
        _meta.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("count", count), ("zone", name)), args.Meta);
        _meta.SetEntityDescription(ent, Loc.GetString(ent.Comp.Description, ("count", count), ("zone", name)), args.Meta);
    }

    private void OnProgress(Entity<FirestakeConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var count = _numbers.GetTarget(ent);
        args.Progress = count > 0 ? Math.Clamp(ent.Comp.Victims.Count / (float) count, 0, 1) : 0;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextCheck)
            return;
        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(0.5);

        var victims = EntityQueryEnumerator<ActiveIgniteOnBuckleComponent, FlammableComponent, BuckleComponent, MindContainerComponent>();
        while (victims.MoveNext(out var uid, out _, out var fire, out var buckle, out var mind))
        {
            if (!fire.OnFire || (!_mobState.IsAlive(uid) && !_mobState.IsCritical(uid)) || mind.Mind is not { } victim ||
                buckle.BuckledTo is not { } stake || !HasComp<IgniteOnBuckleComponent>(stake) ||
                HasComp<BloodBoundComponent>(uid) || _roles.MindHasRole<BloodBoundRoleComponent>(victim))
                continue;

            var xform = Transform(stake);
            if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
            var zones = _zones.GetZones(_zones.GetZoneId(grid, tile));
            var objectives = EntityQueryEnumerator<FirestakeConditionComponent>();
            while (objectives.MoveNext(out var objective, out var condition))
            {
                // don't let someone pre-harvest hearts (or their own) to instantly clear the objective
                if (victim != condition.AssignedMind &&
                    condition.Grid == grid && condition.Zone is { } zone && zones.Contains(zone) &&
                    TryComp<MindComponent>(condition.AssignedMind, out var owner) && owner.Objectives.Contains(objective))
                    condition.Victims.Add(victim);
            }
        }
    }
}
