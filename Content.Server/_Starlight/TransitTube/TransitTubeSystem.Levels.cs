using Content.Shared._Starlight.TransitTube;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Disposal.Tube;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.SubFloor;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private SharedSubFloorHideSystem _subFloor = default!;

    private static readonly ProtoId<DamageTypePrototype> _collapseDamage = "Blunt";

    private const int MaxFloorLayers = 4;

    private readonly HashSet<EntityUid> _supportQueue = [];
    private readonly List<EntityUid> _supportChecking = [];

    private readonly Dictionary<EntityUid, TimeSpan> _sagging = [];
    private readonly List<EntityUid> _collapsing = [];

    private readonly Queue<(EntityUid Tube, int Depth)> _supportFrontier = new();
    private readonly HashSet<EntityUid> _supportSeen = [];

    [SubscribeLocalEvent]
    private void OnTubeMapInit(Entity<TransitTubeComponent> ent, ref MapInitEvent args)
        => QueueSupportCheck(ent);

    [SubscribeLocalEvent]
    private void OnTubeAnchorChanged(Entity<TransitTubeComponent> ent, ref AnchorStateChangedEvent args)
        => QueueSupportCheck(ent);

    [SubscribeLocalEvent]
    private void OnTubeTerminating(Entity<TransitTubeComponent> ent, ref EntityTerminatingEvent args)
    {
        _sagging.Remove(ent);
        QueueSupportCheck(ent);
    }

    [SubscribeLocalEvent]
    private void OnTileChanged(ref TileChangedEvent args)
    {
        // Someone laid a floor: see whether the tubes under it can carry one.
        foreach (var change in args.Changes)
        {
            if (change.NewTile.IsEmpty || !_subFloor.HasFloorCover(args.Entity, args.Entity.Comp, change.GridIndices))
                continue;

            var anchored = _map.GetAnchoredEntities(args.Entity, args.Entity.Comp, change.GridIndices);
            while (anchored.MoveNext(out var uid))
            {
                if (TryComp<TransitTubeComponent>(uid, out var transit) && transit.Level == TransitTubeLevel.Under)
                    _supportQueue.Add(uid.Value);
            }
        }
    }

    private bool IsSupport(Entity<TransitTubeComponent> ent)
        => ent.Comp.Support || HasComp<TransitTubeRampComponent>(ent);

    private void QueueSupportCheck(Entity<TransitTubeComponent> ent)
    {
        if (ent.Comp.Level == TransitTubeLevel.Floor && !IsSupport(ent))
            return;

        _supportQueue.Add(ent);
        WalkLevel(ent, ent.Comp.SupportRange + 1, other =>
        {
            _supportQueue.Add(other);
            return false;
        });
    }

    private bool HasSupport(Entity<TransitTubeComponent> ent)
        => ent.Comp.Level == TransitTubeLevel.Floor
        || IsSupport(ent)
        || WalkLevel(ent, ent.Comp.SupportRange, IsSupport);

    private bool WalkLevel(Entity<TransitTubeComponent> ent, int range, Func<Entity<TransitTubeComponent>, bool> visit)
    {
        _supportFrontier.Clear();
        _supportSeen.Clear();
        _supportSeen.Add(ent);
        _supportFrontier.Enqueue((ent, 0));

        while (_supportFrontier.TryDequeue(out var step))
        {
            if (step.Depth >= range || !TryComp<DisposalTubeComponent>(step.Tube, out var tube))
                continue;

            foreach (var exit in _disposalTube.GetTubeConnectableDirections((step.Tube, tube)))
            {
                if (_disposalTube.GetTubeInDirection((step.Tube, tube), exit) is not { } nextUid
                    || !_supportSeen.Add(nextUid)
                    || TerminatingOrDeleted(nextUid)
                    || !TryComp<TransitTubeComponent>(nextUid, out var next))
                    continue;

                Entity<TransitTubeComponent> nextEnt = (nextUid, next);
                if (visit(nextEnt))
                    return true;

                // The floor carries itself: nothing leans on anything through a ramp.
                if (next.Level != TransitTubeLevel.Floor && !HasComp<TransitTubeRampComponent>(nextUid))
                    _supportFrontier.Enqueue((nextUid, step.Depth + 1));
            }
        }

        return false;
    }

    private void UpdateSupport(TimeSpan now)
    {
        if (_supportQueue.Count > 0)
        {
            _supportChecking.Clear();
            _supportChecking.AddRange(_supportQueue);
            _supportQueue.Clear();

            foreach (var uid in _supportChecking)
            {
                if (!TerminatingOrDeleted(uid) && TryComp<TransitTubeComponent>(uid, out var transit))
                    CheckTubeSupport((uid, transit), now);
            }
        }

        if (_sagging.Count == 0)
            return;

        _collapsing.Clear();
        foreach (var (uid, at) in _sagging)
        {
            if (now >= at)
                _collapsing.Add(uid);
        }

        foreach (var uid in _collapsing)
        {
            _sagging.Remove(uid);
            if (TerminatingOrDeleted(uid) || !TryComp<TransitTubeComponent>(uid, out var transit) || HasSupport((uid, transit)))
                continue;

            Collapse((uid, transit));
        }
    }

    private void CheckTubeSupport(Entity<TransitTubeComponent> ent, TimeSpan now)
    {
        var xform = Transform(ent);
        if (ent.Comp.Level == TransitTubeLevel.Floor || !xform.Anchored || HasSupport(ent))
        {
            _sagging.Remove(ent);
            return;
        }

        if (ent.Comp.Level == TransitTubeLevel.Under)
        {
            CollapseFloor(ent, xform);
            return;
        }

        if (_sagging.TryAdd(ent, now + ent.Comp.CollapseDelay))
            _popup.PopupEntity(Loc.GetString("transit-tube-sagging"), ent, PopupType.MediumCaution);
    }

    private void Collapse(Entity<TransitTubeComponent> ent)
    {
        _popup.PopupCoordinates(Loc.GetString("transit-tube-collapsed"), Transform(ent).Coordinates, PopupType.LargeCaution);
        _audio.PlayPvs(ent.Comp.CollapseSound, Transform(ent).Coordinates);

        // Break it the usual way, so it leaves its shards and rods.
        var damage = new DamageSpecifier(_proto.Index(_collapseDamage), 10000);
        _damageable.ChangeDamage(ent.Owner, damage, ignoreResistances: true);

        if (!TerminatingOrDeleted(ent))
            QueueDel(ent);
    }

    private void CollapseFloor(Entity<TransitTubeComponent> ent, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var indices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var fell = false;
        for (var i = 0; i < MaxFloorLayers && _subFloor.HasFloorCover(gridUid, grid, indices); i++)
        {
            if (!_tile.DeconstructTile(_map.GetTileRef(gridUid, grid, indices)))
                break;

            fell = true;
        }

        if (!fell)
            return;

        _popup.PopupCoordinates(Loc.GetString("transit-tube-floor-collapsed"), xform.Coordinates, PopupType.MediumCaution);
        _audio.PlayPvs(ent.Comp.CollapseSound, xform.Coordinates);
    }

    private void UpdatePodLevel(Entity<TransitPodComponent> pod, Entity<TransitTubeComponent> tube)
    {
        var hidden = tube.Comp.Level == TransitTubeLevel.Under
            && TryComp<SubFloorHideComponent>(tube, out var hide)
            && hide.IsUnderCover;

        _appearance.SetData(pod, TransitPodVisuals.Level, tube.Comp.Level);
        _appearance.SetData(pod, TransitPodVisuals.Hidden, hidden);
    }
}
