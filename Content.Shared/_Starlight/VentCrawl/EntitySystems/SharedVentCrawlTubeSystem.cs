using System.Linq;
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

namespace Content.Shared.VentCrawl;

public sealed partial class SharedVentCrawlTubeSystem : EntitySystem
{
    [Dependency] private SharedVentCrawlableSystem _ventCrawableSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlTubeComponent, ComponentInit>(OnComponentInit);
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
    }

    #region Subscribes

    private void OnComponentInit(EntityUid uid, VentCrawlTubeComponent tube, ComponentInit args)
        => tube.Contents = _containerSystem.EnsureContainer<Container>(uid, tube.ContainerId);

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

        TryInsert(args.Args.Target.Value, args.Args.Used.Value);

        args.Handled = true;
    }

    private void OnGetBendConnectableDirections(EntityUid uid, VentCrawlBendComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
    {
        var direction = Transform(uid).LocalRotation;
        var side = new Angle(MathHelper.DegreesToRadians(direction.Degrees - 90));

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

        foreach (var entity in tube.Contents.ContainedEntities.ToArray())
            _ventCrawableSystem.ExitVentCrawl(entity);
    }

    public EntityUid? NextTubeFor(EntityUid target, Direction nextDirection, VentCrawlTubeComponent? targetTube = null)
    {
        if (!Resolve(target, ref targetTube))
            return null;
        var oppositeDirection = nextDirection.GetOpposite();

        var xform = Transform(target);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        if (xform.GridUid == null)
            return null;

        var position = xform.Coordinates;
        foreach (EntityUid entity in _mapSystem.GetInDir(xform.GridUid.Value, grid, position, nextDirection))
        {

            if (!TryComp(entity, out VentCrawlTubeComponent? tube)
                || !CanConnect(target, targetTube, nextDirection)
                || !CanConnect(entity, tube, oppositeDirection))
                continue;

            return entity;
        }

        return null;
    }

    private bool CanConnect(EntityUid tubeId, VentCrawlTubeComponent tube, Direction direction)
    {
        if (!tube.Connected)
        {
            return false;
        }

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

        var holder = Spawn(VentCrawlEntryComponent.HolderPrototypeId, _transform.GetMapCoordinates(uid));
        var holderComponent = Comp<VentCrawlHolderComponent>(holder);

        _ventCrawableSystem.TryInsert(holder, entity, holderComponent);

        _mover.SetRelay(entity, holder);
        ventCrawlerComponent.InTube = true;
        Dirty(entity, ventCrawlerComponent);

        return _ventCrawableSystem.EnterTube(holder, uid, holderComponent);
    }

    #endregion
}
