using System.Linq;
using Content.Shared.Atmos.Components;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Tools.Components;
using Content.Shared._Starlight.VentCrawl.Components;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;

namespace Content.Shared._Starlight.VentCrawl.EntitySystems;

public sealed partial class SharedVentCrawlSystem
{
    public void InitializeTubes()
    {
        SubscribeLocalEvent<VentCrawlTubeComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<VentCrawlTubeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VentCrawlTubeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VentCrawlTubeComponent, AnchorStateChangedEvent>(OnAnchorChange);
        SubscribeLocalEvent<VentCrawlTubeComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<VentCrawlTubeComponent, EntityTerminatingEvent>(OnTerminating);

        SubscribeLocalEvent<VentCrawlEntryComponent, GetVerbsEvent<AlternativeVerb>>(AddClimbedVerb);
        SubscribeLocalEvent<VentCrawlerComponent, EnterVentDoAfterEvent>(OnDoAfterEnterTube);

        SubscribeLocalEvent<VentCrawlBendComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetBendConnectableDirections);
        SubscribeLocalEvent<VentCrawlEntryComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetEntryConnectableDirections);
        SubscribeLocalEvent<VentCrawlJunctionComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetJunctionConnectableDirections);
        SubscribeLocalEvent<VentCrawlTransitComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetTransitConnectableDirections);
        SubscribeLocalEvent<VentCrawlManifoldComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetManifoldConnectableDirections);
    }

    #region Subscribes
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

    private void OnTerminating(EntityUid uid, VentCrawlTubeComponent component, ref EntityTerminatingEvent args)
        => DisconnectTube(component);

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
            TryInsert(args.Args.Target.Value, args.Args.Used.Value, null, component);

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

        foreach (var holder in tube.ContainedHolders)
            ExitVentCrawl(holder);
    }

    public EntityUid? GetManifoldExit(
        EntityUid manifoldUid,
        AtmosPipeLayer currentLayer,
        Direction direction)
    {
        var xform = Transform(manifoldUid);
        if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        var position = xform.Coordinates;

        foreach (var entity in _mapSystem.GetInDir(xform.GridUid.Value, grid, position, direction))
        {
            if (!TryComp<VentCrawlTubeComponent>(entity, out var tube))
                continue;

            if (!tube.Connected)
                continue;

            if (!SameLayer(currentLayer, entity))
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

            if (!IsMutuallyConnected(target, targetTube, entity, tube, nextDirection))
                continue;

            if (!IsSameAxis(target, entity, nextDirection))
                continue;

            return entity;
        }

        return null;
    }

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

        return hasB && a == lb!.CurrentPipeLayer;
    }

    private bool CanConnect(EntityUid tubeId, VentCrawlTubeComponent tube, Direction direction)
    {
        if (!tube.Connected)
            return false;

        var ev = new GetVentCrawlsConnectableDirectionsEvent();
        RaiseLocalEvent(tubeId, ref ev);
        return ev.Connectable.Contains(direction);
    }

    private bool IsMutuallyConnected(
        EntityUid fromUid,
        VentCrawlTubeComponent fromTube,
        EntityUid toUid,
        VentCrawlTubeComponent toTube,
        Direction moveDir)
    {
        var opposite = moveDir.GetOpposite();

        if (!CanConnect(fromUid, fromTube, moveDir))
            return false;

        if (!CanConnect(toUid, toTube, opposite))
            return false;

        return true;
    }

    private bool SupportsDirection(EntityUid uid, Direction dir)
    {
        var ev = new GetVentCrawlsConnectableDirectionsEvent();
        RaiseLocalEvent(uid, ref ev);

        return ev.Connectable.Contains(dir);
    }

    private bool IsSameAxis(EntityUid from, EntityUid to, Direction moveDir)
    {
        var opposite = moveDir.GetOpposite();

        return SupportsDirection(from, moveDir)
               && SupportsDirection(to, opposite);
    }

    public bool TryInsert(EntityUid entry, EntityUid target, VentCrawlEntryComponent? entryComp = null, VentCrawlerComponent? ventCrawler = null)
    {
        if (!Resolve(entry, ref entryComp))
            return false;

        if (!Resolve(target, ref ventCrawler))
            return false;

        var tubeCoords = Transform(entry).Coordinates;
        var holder = PredictedSpawnAttachedTo(VentCrawlEntryComponent.HolderPrototypeId, tubeCoords);
        var holderComponent = Comp<VentCrawlHolderComponent>(holder);

        if (!TryInsert(holder, target))
        {
            Del(holder);
            return false;
        }

        _mover.SetRelay(target, holder);
        ventCrawler.InTube = true;
        Dirty(target, ventCrawler);
        holderComponent.ContainedEntity = target;
        DirtyField(holder, holderComponent, nameof(VentCrawlHolderComponent.ContainedEntity));

        var result = EnterTube(holder, entry, holderComponent);

        if (!result)
        {
            ExitVentCrawl(holder, holderComponent);
            return false;
        }

        return true;
    }

    #endregion
}
