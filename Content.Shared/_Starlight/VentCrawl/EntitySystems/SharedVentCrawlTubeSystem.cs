using System.Linq;
using Content.Shared.Atmos.Components;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.VentCrawl.Components;
using Content.Shared.VentCrawl.EntitySystems;
using Content.Shared.VentCrawl.Tube.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;

namespace Content.Shared.VentCrawl;

public sealed partial class SharedVentCrawlTubeSystem : EntitySystem
{
    [Dependency] private SharedVentCrawlableSystem _ventCrawableSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlTubeComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<VentCrawlTubeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VentCrawlTubeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VentCrawlTubeComponent, AnchorStateChangedEvent>(OnAnchorChange);
        SubscribeLocalEvent<VentCrawlTubeComponent, BreakageEventArgs>(OnBreak);

        SubscribeLocalEvent<VentCrawlEntryComponent, GetVerbsEvent<AlternativeVerb>>(AddClimbedVerb);
        SubscribeLocalEvent<VentCrawlerComponent, EnterVentDoAfterEvent>(OnDoAfterEnterTube);

        SubscribeLocalEvent<VentCrawlBendComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetBendConnectableDirections);
        SubscribeLocalEvent<VentCrawlEntryComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetEntryConnectableDirections);
        SubscribeLocalEvent<VentCrawlJunctionComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetJunctionConnectableDirections);
        SubscribeLocalEvent<VentCrawlTransitComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetTransitConnectableDirections);
        SubscribeLocalEvent<VentCrawlManifoldComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetManifoldConnectableDirections);
    }

    #region Subscribes

    public Container GetOrEnsureContainer(EntityUid uid, VentCrawlTubeComponent tube)
        => _containerSystem.EnsureContainer<Container>(uid, tube.ContainerId);

    private void OnComponentRemove(EntityUid uid, VentCrawlTubeComponent tube, ComponentRemove args)
        => DisconnectTube(tube);

    private void OnShutdown(EntityUid uid, VentCrawlTubeComponent tube, ComponentShutdown args)
        => DisconnectTube(tube);

    private void OnStartup(EntityUid uid, VentCrawlTubeComponent component, ComponentStartup args)
        => UpdateAnchored(component, Transform(uid).Anchored);

    private void OnBreak(EntityUid uid, VentCrawlTubeComponent component, BreakageEventArgs args)
        => DisconnectTube(component);

    private void OnAnchorChange(EntityUid uid, VentCrawlTubeComponent component, ref AnchorStateChangedEvent args)
        => UpdateAnchored(component, args.Anchored);

    private void AddClimbedVerb(EntityUid uid, VentCrawlEntryComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<VentCrawlerComponent>(args.User, out var ventCrawlerComponent) || HasComp<BeingVentCrawlComponent>(args.User))
            return;

        var xform = Transform(uid);

        if (!xform.Anchored)
            return;

        AlternativeVerb verb = new()
        {
            Act = () => TryEnter(uid, args.User, ventCrawlerComponent),
            Text = Loc.GetString("comp-climbable-verb-climb")
        };
        args.Verbs.Add(verb);
    }

    private void OnDoAfterEnterTube(EntityUid uid, VentCrawlerComponent component, EnterVentDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null || args.Args.Used == null)
            return;

        if (_net.IsServer)
            TryInsert(args.Args.Target.Value, args.Args.Used.Value);

        args.Handled = true;
    }

    private void OnGetBendConnectableDirections(EntityUid uid, VentCrawlBendComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
    {
        var direction = Transform(uid).LocalRotation;
        var side = direction - Angle.FromDegrees(90);

        args.Connectable = new[] { direction.GetDir(), side.GetDir() };
    }

    private void OnGetEntryConnectableDirections(EntityUid uid, VentCrawlEntryComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
        => args.Connectable = new[] { Transform(uid).LocalRotation.GetDir() };

    private void OnGetJunctionConnectableDirections(EntityUid uid, VentCrawlJunctionComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
    {
        var direction = Transform(uid).LocalRotation;

        args.Connectable = component.Degrees
            .Select(degree => new Angle(degree.Theta + direction.Theta).GetDir())
            .ToArray();
    }

    private void OnGetTransitConnectableDirections(EntityUid uid, VentCrawlTransitComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
    {
        var rotation = Transform(uid).LocalRotation;
        var opposite = new Angle(rotation.Theta + Math.PI);

        args.Connectable = new[] { rotation.GetDir(), opposite.GetDir() };
    }

    private void OnGetManifoldConnectableDirections(EntityUid uid, VentCrawlManifoldComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
        => args.Connectable = new[] { Direction.North, Direction.South, Direction.East, Direction.West };

    #endregion

    #region Helpers

    private void TryEnter(EntityUid uid, EntityUid user, VentCrawlerComponent crawler)
    {
        if (TryComp<WeldableComponent>(uid, out var weldableComponent))
        {
            if (weldableComponent.IsWelded)
            {
                _popup.PopupPredicted(Loc.GetString("entity-storage-component-welded-shut-message"), user, null);
                return;
            }
        }

        var args = new DoAfterArgs(EntityManager, user, crawler.EnterDelay, new EnterVentDoAfterEvent(), user, uid, user)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        _doAfterSystem.TryStartDoAfter(args);
    }

    private void UpdateAnchored(VentCrawlTubeComponent component, bool anchored)
    {
        if (anchored)
            ConnectTube(component);
        else
            DisconnectTube(component);
    }

    private static void ConnectTube(VentCrawlTubeComponent tube)
    {
        if (tube.Connected)
            return;

        tube.Connected = true;
    }

    private void DisconnectTube(VentCrawlTubeComponent tube)
    {
        if (!tube.Connected)
            return;

        tube.Connected = false;

        foreach (var entity in GetOrEnsureContainer(tube.Owner, tube).ContainedEntities.ToArray())
            _ventCrawableSystem.ExitVentCrawl(entity);
    }

    public EntityUid? GetManifoldExit(
        EntityUid manifoldUid,
        int currentLayer,
        Direction direction)
    {
        var xform = Transform(manifoldUid);
        if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        var targetLayer = TransformFromManifoldLayer(currentLayer);
        var position = xform.Coordinates;

        foreach (var entity in _mapSystem.GetInDir(xform.GridUid.Value, grid, position, direction))
        {
            if (!TryComp<VentCrawlTubeComponent>(entity, out var tube))
                continue;

            if (!tube.Connected)
                continue;

            if (!SameLayer(targetLayer, entity))
                continue;

            if (!CanConnect(entity, tube, direction.GetOpposite()))
                continue;

            return entity;
        }

        return null;
    }

    public EntityUid? NextTubeFor(EntityUid target, Direction nextDirection, VentCrawlTubeComponent? targetTube = null)
    {
        if (!Resolve(target, ref targetTube))
            return null;

        var oppositeDirection = nextDirection.GetOpposite();

        var xform = Transform(target);

        if (xform.GridUid == null ||
            !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        var position = xform.Coordinates;

        foreach (var entity in _mapSystem.GetInDir(xform.GridUid.Value, grid, position, nextDirection))
        {
            if (!TryComp(entity, out VentCrawlTubeComponent? tube))
                continue;

            if (!HasComp<VentCrawlManifoldComponent>(entity) && !SameLayer(target, entity))
                continue;

            if (!CanConnect(target, targetTube, nextDirection))
                continue;

            if (!CanConnect(entity, tube, oppositeDirection))
                continue;

            return entity;
        }

        return null;
    }

    public static AtmosPipeLayer TransformFromManifoldLayer(int layer) => layer switch
    {
        2 => AtmosPipeLayer.Primary,

        1 => AtmosPipeLayer.Secondary,
        3 => AtmosPipeLayer.Tertiary,

        0 => AtmosPipeLayer.Quaternary,
        4 => AtmosPipeLayer.Quinary,

        _ => AtmosPipeLayer.Primary
    };

    private bool SameLayer(EntityUid a, EntityUid b)
    {
        var hasA = TryComp(a, out AtmosPipeLayersComponent? la);
        var hasB = TryComp(b, out AtmosPipeLayersComponent? lb);

        if (hasA != hasB)
            return false;

        return !hasA || la!.CurrentPipeLayer == lb!.CurrentPipeLayer;
    }

    private bool SameLayer(AtmosPipeLayer a, EntityUid b)
    {
        var hasB = TryComp(b, out AtmosPipeLayersComponent? lb);

        return !hasB ? false : a == lb!.CurrentPipeLayer;
    }

    private bool CanConnect(EntityUid tubeId, VentCrawlTubeComponent tube, Direction direction)
    {
        if (!tube.Connected)
            return false;

        var ev = new GetVentCrawlsConnectableDirectionsEvent();
        RaiseLocalEvent(tubeId, ref ev);
        return ev.Connectable.Contains(direction);
    }

    public bool TryInsert(EntityUid uid, EntityUid entity, VentCrawlEntryComponent? entry = null)
    {
        if (!Resolve(uid, ref entry))
            return false;

        if (!TryComp<VentCrawlerComponent>(entity, out var ventCrawlerComponent))
            return false;

        var tubeCoords = Transform(uid).Coordinates;
        var holder = PredictedSpawnAttachedTo(VentCrawlEntryComponent.HolderPrototypeId, tubeCoords);
        var holderComponent = Comp<VentCrawlHolderComponent>(holder);

        if (!_ventCrawableSystem.TryInsert(holder, entity))
        {
            Del(holder);
            return false;
        }

        _mover.SetRelay(entity, holder);
        ventCrawlerComponent.InTube = true;
        Dirty(entity, ventCrawlerComponent);

        var result = _ventCrawableSystem.EnterTube(holder, uid, holderComponent);

        if (!result)
        {
            _ventCrawableSystem.ExitVentCrawl(holder, holderComponent);
            return false;
        }

        return true;
    }

    #endregion
}
