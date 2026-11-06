using System.Linq;
using Content.Server.Buckle.Systems;
using Content.Server._Starlight.Antags.BloodBound.Systems;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Server._Starlight.Objectives.Components;
using Content.Server._Starlight.Roles;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Antags.BloodBound.Components;
using Content.Shared._Starlight.Mind.Events;
using Content.Shared._Starlight.Zones;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.GameTicking;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

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
    private readonly Dictionary<(EntityUid Grid, ProtoId<ZonePrototype> Zone), HashSet<Entity<FirestakeConditionComponent>>> _activeByZone = new();
    private readonly Dictionary<EntityUid, HashSet<Entity<FirestakeConditionComponent>>> _activeByMind = new();
    private readonly Dictionary<EntityUid, bool> _trackedStakes = new(); // so we don't have to track all stakes always
    private readonly HashSet<EntityUid> _pendingVictims = [];
    private bool _checkQueued;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FirestakeConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<FirestakeConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<FirestakeConditionComponent, ObjectiveGetProgressEvent>(OnProgress);
        SubscribeLocalEvent<FirestakeConditionComponent, ComponentShutdown>(OnObjectiveShutdown);
        SubscribeLocalEvent<FirestakeObjectivesComponent, MindObjectivesChangedEvent>(OnObjectivesChanged);
        SubscribeLocalEvent<FirestakeObjectivesComponent, ComponentShutdown>(OnMindShutdown);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemoved);
        SubscribeLocalEvent<StrapComponent, StrappedEvent>(OnStrapped, after: [typeof(IgniteOnBuckleSystem)]);
        SubscribeLocalEvent<StrapComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<IgniteOnBuckleComponent, ComponentShutdown>(OnStakeShutdown);
        SubscribeLocalEvent<IgniteOnBuckleComponent, MoveEvent>(OnStakeMoved);
        SubscribeLocalEvent<IgniteOnBuckleComponent, ZoneMembershipChangedEvent>(OnZoneChanged);
        SubscribeLocalEvent<ActiveIgniteOnBuckleComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<ActiveIgniteOnBuckleComponent, ComponentStartup>(OnActiveStartup);
        SubscribeLocalEvent<ActiveIgniteOnBuckleComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ActiveIgniteOnBuckleComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ActiveIgniteOnBuckleComponent, BloodBoundRemovedEvent>(OnBloodBoundRemoved);
        SubscribeLocalEvent<CheckVictimsEvent>(OnCheckVictims);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
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
        if (grid is not { } gridUid || _zones.GetGridZones(gridUid) is not { Count: > 0 } zones)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.AssignedMind = args.MindId;
        ent.Comp.Grid = gridUid;
        ent.Comp.Zone = _random.Pick(zones);
        EnsureComp<FirestakeObjectivesComponent>(args.MindId);
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

    private bool IsComplete(Entity<FirestakeConditionComponent> ent)
        => _numbers.GetTarget(ent) <= ent.Comp.Victims.Count;

    private void OnObjectivesChanged(Entity<FirestakeObjectivesComponent> ent, ref MindObjectivesChangedEvent args)
    {
        if (!TryComp<MindComponent>(ent, out var mind))
            return;

        if (_activeByMind.TryGetValue(ent.Owner, out var previous))
        {
            foreach (var objective in previous.ToArray())
            {
                if (!mind.Objectives.Contains(objective.Owner) || IsComplete(objective))
                    RemoveActive(objective);
            }
        }

        var added = false;
        foreach (var uid in mind.Objectives)
        {
            if (!TryComp<FirestakeConditionComponent>(uid, out var condition) ||
                condition.AssignedMind != ent.Owner || condition.Grid is not { } grid ||
                condition.Zone is not { } zone || IsComplete((uid, condition)))
                continue;

            Entity<FirestakeConditionComponent> objective = (uid, condition);
            if (!_activeByZone.GetOrNew((grid, zone)).Add(objective))
                continue;

            _activeByMind.GetOrNew(ent.Owner).Add(objective);
            added = true;
        }

        if (!added)
            return;

        // corner case for people who are already on a firestake when you take the objective
        var victims = EntityQueryEnumerator<ActiveIgniteOnBuckleComponent, BuckleComponent>();
        while (victims.MoveNext(out var uid, out _, out var buckle))
        {
            if (buckle.BuckledTo is not { } stake || !HasComp<IgniteOnBuckleComponent>(stake))
                continue;

            TrackStake(stake);
            QueueVictim(uid);
        }
    }

    private void RemoveActive(Entity<FirestakeConditionComponent> ent)
    {
        if (ent.Comp.Grid is { } grid && ent.Comp.Zone is { } zone &&
            _activeByZone.TryGetValue((grid, zone), out var objectives))
        {
            objectives.Remove(ent);
            if (objectives.Count == 0)
                _activeByZone.Remove((grid, zone));
        }

        if (ent.Comp.AssignedMind is { } mind && _activeByMind.TryGetValue(mind, out var owned))
        {
            owned.Remove(ent);
            if (owned.Count == 0)
                _activeByMind.Remove(mind);
        }

        if (_activeByZone.Count == 0)
            StopTracking();
    }

    private void OnObjectiveShutdown(Entity<FirestakeConditionComponent> ent, ref ComponentShutdown args)
        => RemoveActive(ent);

    private void OnMindShutdown(Entity<FirestakeObjectivesComponent> ent, ref ComponentShutdown args)
    {
        if (!_activeByMind.TryGetValue(ent.Owner, out var objectives))
            return;

        foreach (var objective in objectives.ToArray())
            RemoveActive(objective);
    }

    private void TrackStake(EntityUid stake)
    {
        if (_activeByZone.Count == 0 || _trackedStakes.ContainsKey(stake))
            return;

        var addedTracker = !HasComp<ZoneTrackerComponent>(stake);
        _trackedStakes.Add(stake, addedTracker);
        if (addedTracker)
            EnsureComp<ZoneTrackerComponent>(stake);
    }

    private void UntrackStake(EntityUid stake)
    {
        if (_trackedStakes.Remove(stake, out var addedTracker) && addedTracker && !TerminatingOrDeleted(stake))
            RemComp<ZoneTrackerComponent>(stake);
    }

    private void StopTracking()
    {
        foreach (var stake in _trackedStakes.Keys.ToArray())
            UntrackStake(stake);
        _pendingVictims.Clear();
    }

    private void OnStrapped(Entity<StrapComponent> ent, ref StrappedEvent args)
    {
        if (!HasComp<IgniteOnBuckleComponent>(ent))
            return;

        TrackStake(ent.Owner);
        QueueVictim(args.Buckle);
    }

    private void OnUnstrapped(Entity<StrapComponent> ent, ref UnstrappedEvent args)
    {
        if (ent.Comp.BuckledEntities.Count == 0)
            UntrackStake(ent.Owner);
    }

    private void OnStakeShutdown(Entity<IgniteOnBuckleComponent> ent, ref ComponentShutdown args)
        => UntrackStake(ent.Owner);

    private void OnStakeMoved(Entity<IgniteOnBuckleComponent> ent, ref MoveEvent args)
        => QueueStake(ent.Owner);

    private void OnZoneChanged(Entity<IgniteOnBuckleComponent> ent, ref ZoneMembershipChangedEvent args)
        => QueueStake(ent.Owner);

    private void QueueStake(EntityUid stake)
    {
        if (_activeByZone.Count == 0 || !TryComp<StrapComponent>(stake, out var strap) || strap.BuckledEntities.Count == 0)
            return;

        TrackStake(stake);
        foreach (var victim in strap.BuckledEntities)
            QueueVictim(victim);
    }

    private void OnIgnited(Entity<ActiveIgniteOnBuckleComponent> ent, ref IgnitedEvent args) => QueueVictim(ent.Owner);
    private void OnActiveStartup(Entity<ActiveIgniteOnBuckleComponent> ent, ref ComponentStartup args) => QueueVictim(ent.Owner);
    private void OnMobStateChanged(Entity<ActiveIgniteOnBuckleComponent> ent, ref MobStateChangedEvent args) => QueueVictim(ent.Owner);
    private void OnMindAdded(Entity<ActiveIgniteOnBuckleComponent> ent, ref MindAddedMessage args) => QueueVictim(ent.Owner);
    private void OnBloodBoundRemoved(Entity<ActiveIgniteOnBuckleComponent> ent, ref BloodBoundRemovedEvent args) => QueueVictim(ent.Owner);
    private void OnRoleAdded(RoleAddedEvent args) => QueueMind((args.MindId, args.Mind));
    private void OnRoleRemoved(RoleRemovedEvent args) => QueueMind((args.MindId, args.Mind));

    private void QueueMind(Entity<MindComponent> ent)
    {
        if (ent.Comp.OwnedEntity is { } body)
            QueueVictim(body);
    }

    private void QueueVictim(EntityUid victim)
    {
        if (_activeByZone.Count == 0 || !HasComp<ActiveIgniteOnBuckleComponent>(victim))
            return;

        _pendingVictims.Add(victim);
        if (_checkQueued)
            return;

        // Check all pending victims
        _checkQueued = true;
        QueueLocalEvent(new CheckVictimsEvent());
    }

    private void OnCheckVictims(CheckVictimsEvent args)
    {
        _checkQueued = false;
        var victims = _pendingVictims.ToArray();
        _pendingVictims.Clear();
        foreach (var victim in victims)
            CheckVictim(victim);
    }

    private void CheckVictim(EntityUid uid)
    {
        if (_activeByZone.Count == 0 || !HasComp<ActiveIgniteOnBuckleComponent>(uid) ||
            !TryComp<FlammableComponent>(uid, out var fire) || !fire.OnFire ||
            (!_mobState.IsAlive(uid) && !_mobState.IsCritical(uid)) ||
            !TryComp<MindContainerComponent>(uid, out var mind) || mind.Mind is not { } victim ||
            !TryComp<BuckleComponent>(uid, out var buckle) || buckle.BuckledTo is not { } stake ||
            !HasComp<IgniteOnBuckleComponent>(stake) || HasComp<BloodBoundComponent>(uid) ||
            _roles.MindHasRole<BloodBoundRoleComponent>(victim))
            return;

        TrackStake(stake);
        var xform = Transform(stake);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        foreach (var zone in _zones.GetZones(_zones.GetZoneId(grid, tile)))
        {
            if (!_activeByZone.TryGetValue((grid, zone), out var objectives))
                continue;

            foreach (var objective in objectives.ToArray())
            {
                if (victim == objective.Comp.AssignedMind || !objective.Comp.Victims.Add(victim))
                    continue;

                if (IsComplete(objective))
                    RemoveActive(objective);
            }
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _activeByZone.Clear();
        _activeByMind.Clear();
        StopTracking();
        _checkQueued = false;
    }

    private sealed class CheckVictimsEvent : EntityEventArgs;
}
