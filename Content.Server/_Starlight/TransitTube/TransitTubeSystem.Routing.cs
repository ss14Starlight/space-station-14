using Content.Shared._Starlight.TransitTube;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Disposal.Tube;
using Content.Shared.Labels.Components;
using Content.Shared.Verbs;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    /// <summary>
    /// How many (tube, direction) states a route search may look at before it gives up.
    /// </summary>
    private const int MaxRouteSearch = 4096;

    private readonly Dictionary<(EntityUid Tube, Direction Travel), (EntityUid Tube, Direction Travel, Direction Exit)> _cameFrom = [];
    private readonly Queue<(EntityUid Tube, Direction Travel)> _frontier = new();
    private readonly List<Direction> _choices = [];

    #region Planning

    /// <summary>
    /// Plans the trip of a pod about to leave a station: to its target, or to the nearest free station.
    /// </summary>
    /// <returns>False if there is no way there; a pod with a target then forgets it and runs as usual.</returns>
    private bool TryPlanTrip(Entity<TransitPodComponent> pod, Entity<TransitTubeStationComponent> station)
    {
        if (pod.Comp.Target is { } target)
        {
            if (TryFindRoute(station, pod, uid => uid == target, out _, out _))
                return true;

            CancelTrip(pod);
            PopupPassengers(pod, "transit-pod-no-route");
            return false;
        }

        if (!pod.Comp.AnyFree)
            return false;

        if (!TryFindRoute(station, pod, uid => IsFreeStation(uid, pod), out var goal, out _)
            || !TryComp<TransitTubeStationComponent>(goal, out var goalStation))
            return false;

        pod.Comp.Target = goal;
        Reserve((goal, goalStation), pod);
        Dirty(pod);
        return true;
    }

    private void CancelTrip(Entity<TransitPodComponent> pod)
    {
        pod.Comp.Target = null;
        pod.Comp.AnyFree = false;
        pod.Comp.Plan.Clear();
        ReleaseReservation(pod);
        Dirty(pod);
    }

    private bool IsFreeStation(EntityUid uid, EntityUid pod)
        => TryComp<TransitTubeStationComponent>(uid, out var station)
        && !station.Depot
        && CanDockAt((uid, station), pod);

    /// <summary>
    /// Breadth-first search over (tube, direction of travel) from a station to the nearest tube
    /// matching <paramref name="isGoal"/>, taking only the turns a pod can actually make.
    /// </summary>
    /// <param name="pod">Gets the found route as its plan. Null only checks that there is one.</param>
    /// <param name="length">How many tubes the found route runs through.</param>
    private bool TryFindRoute(EntityUid origin, Entity<TransitPodComponent>? pod, Func<EntityUid, bool> isGoal, out EntityUid goal, out int length)
    {
        goal = default;
        length = 0;
        _cameFrom.Clear();
        _frontier.Clear();

        if (!TryComp<DisposalTubeComponent>(origin, out var originTube))
            return false;

        foreach (var exit in _disposalTube.GetTubeConnectableDirections((origin, originTube)))
        {
            if (_disposalTube.GetTubeInDirection((origin, originTube), exit) is not { } next)
                continue;

            if (_cameFrom.TryAdd((next, exit), (origin, Direction.Invalid, exit)))
                _frontier.Enqueue((next, exit));
        }

        var searched = 0;
        while (searched++ < MaxRouteSearch && _frontier.TryDequeue(out var state))
        {
            if (state.Tube != origin && isGoal(state.Tube))
            {
                goal = state.Tube;
                length = RouteLength(origin, state);
                if (pod is { } podEnt)
                    WritePlan(podEnt, origin, state);
                return true;
            }

            if (!TryComp<DisposalTubeComponent>(state.Tube, out var tube))
                continue;

            GetChoices(state.Tube, tube, state.Travel, _choices);
            foreach (var exit in _choices)
            {
                if (_disposalTube.GetTubeInDirection((state.Tube, tube), exit) is not { } next)
                    continue;

                if (_cameFrom.TryAdd((next, exit), (state.Tube, state.Travel, exit)))
                    _frontier.Enqueue((next, exit));
            }
        }

        return false;
    }

    private void GetChoices(EntityUid uid, DisposalTubeComponent tube, Direction travel, List<Direction> choices)
    {
        choices.Clear();

        if (!TryComp<TransitTubeComponent>(uid, out var transit))
            return;

        Entity<TransitTubeComponent> ent = (uid, transit);
        var rotation = Transform(uid).LocalRotation;

        if (transit.OneWay is { } oneWay && !SameWay(travel, oneWay, rotation) && IsEnabled(uid) && !IsEmagged(uid))
        {
            choices.Add(travel.GetOpposite());
            return;
        }

        if (HasComp<TransitTubeStationComponent>(uid))
        {
            var through = PickExit((uid, transit, tube), travel);
            if (through != Direction.Invalid)
                choices.Add(through);
            return;
        }

        if (TryComp<TransitTubeTurntableComponent>(uid, out var turntable))
        {
            var turned = Turn(ent, tube, turntable, travel);
            if (turned != Direction.Invalid)
                choices.Add(turned);
            return;
        }

        var back = travel.GetOpposite();
        foreach (var exit in _disposalTube.GetTubeConnectableDirections((uid, tube)))
        {
            if (exit == back)
                continue;

            if (Math.Abs(Angle.ShortestDistance(travel.ToAngle(), exit.ToAngle()).Degrees) > transit.MaxTurn + 1)
                continue;

            choices.Add(exit);
        }
    }

    private int RouteLength(EntityUid origin, (EntityUid Tube, Direction Travel) goal)
    {
        var length = 0;
        var state = goal;
        while (_cameFrom.TryGetValue(state, out var from) && length < MaxRouteSearch)
        {
            length++;
            if (from.Tube == origin && from.Travel == Direction.Invalid)
                break;

            state = (from.Tube, from.Travel);
        }

        return length;
    }

    private void WritePlan(Entity<TransitPodComponent> pod, EntityUid origin, (EntityUid Tube, Direction Travel) goal)
    {
        pod.Comp.Plan.Clear();
        Dirty(pod);

        var state = goal;
        while (_cameFrom.TryGetValue(state, out var from))
        {
            pod.Comp.Plan[(from.Tube, from.Travel)] = from.Exit;

            // The way out of the origin is stored with no direction of travel.
            if (from.Tube == origin && from.Travel == Direction.Invalid)
                break;

            state = (from.Tube, from.Travel);
        }
    }

    #endregion

    #region Calling pods

    /// <summary>
    /// Sends the nearest pod parked at a depot to a station that has none.
    /// </summary>
    private bool TryCallPod(Entity<TransitTubeStationComponent> station, EntityUid user)
    {
        if (station.Comp.DockedPod != null || station.Comp.Depot)
            return false;

        // A pod is already on its way here: don't drain the depots calling more.
        if (station.Comp.ReservedBy is { } reserved
            && TryComp<TransitPodComponent>(reserved, out var coming)
            && coming.Reserved == station.Owner)
        {
            _popup.PopupEntity(Loc.GetString("transit-station-pod-coming"), station, user);
            return true;
        }

        // Send the parked pod with the shortest way here.
        Entity<TransitPodComponent>? best = null;
        EntityUid bestDepot = default;
        var bestLength = int.MaxValue;

        var query = EntityQueryEnumerator<TransitTubeStationComponent>();
        while (query.MoveNext(out var depotUid, out var depot))
        {
            if (!depot.Depot
                || depot.DockedPod is not { } podUid
                || !TryComp<TransitPodComponent>(podUid, out var pod)
                || pod.State != TransitPodState.Open
                || pod.Target != null)
                continue;

            if (!TryFindRoute(depotUid, null, uid => uid == station.Owner, out _, out var length) || length >= bestLength)
                continue;

            best = (podUid, pod);
            bestDepot = depotUid;
            bestLength = length;
        }

        if (best is not { } chosen || !TryFindRoute(bestDepot, chosen, uid => uid == station.Owner, out _, out _))
            return false;

        chosen.Comp.Target = station;
        Reserve(station, chosen);
        chosen.Comp.NextDeparture = _timing.CurTime;
        Dirty(chosen);
        _popup.PopupEntity(Loc.GetString("transit-station-pod-called"), station, user);
        return true;
    }

    #endregion

    #region Destination picker

    [SubscribeLocalEvent]
    private void OnStationVerbs(Entity<TransitTubeStationComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Depot)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("transit-station-verb-destination"),
            Act = () => OpenDestinations(ent, user),
        });
    }

    private void OpenDestinations(Entity<TransitTubeStationComponent> station, EntityUid user)
    {
        UpdateDestinations(station);
        _ui.OpenUi(station.Owner, TransitStationUiKey.Key, user);
    }

    private void UpdateDestinations(Entity<TransitTubeStationComponent> station)
    {
        // One search over the whole line, picking up every named station on the way.
        var destinations = new List<TransitDestinationEntry>();
        var found = new HashSet<EntityUid>();
        TryFindRoute(station, null, uid =>
        {
            if (uid != station.Owner
                && TryComp<TransitTubeStationComponent>(uid, out var other)
                && !other.Depot
                && GetDestinationName((uid, other)) is { } name
                && found.Add(uid))
                destinations.Add(new TransitDestinationEntry(GetNetEntity(uid), name));

            return false;
        }, out _, out _);

        destinations.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        TransitPodComponent? pod = null;
        if (station.Comp.DockedPod is { } podUid)
            TryComp(podUid, out pod);

        var state = new TransitStationBoundUserInterfaceState(destinations, GetNetEntity(pod?.Target), pod?.AnyFree ?? false);
        _ui.SetUiState(station.Owner, TransitStationUiKey.Key, state);
    }

    /// <summary>
    /// The name a station is listed under: its <see cref="TransitTubeStationComponent.Destination"/>,
    /// otherwise its label.
    /// </summary>
    public string? GetDestinationName(Entity<TransitTubeStationComponent> station)
    {
        if (station.Comp.Destination is { } destination)
            return Loc.GetString(destination);

        return CompOrNull<LabelComponent>(station)?.CurrentLabel is { Length: > 0 } label ? label : null;
    }

    [SubscribeLocalEvent]
    private void OnSelectDestination(Entity<TransitTubeStationComponent> ent, ref TransitStationSelectDestinationMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent))
        {
            _popup.PopupEntity(Loc.GetString("transit-station-access-denied"), ent, args.Actor);
            UpdateDestinations(ent);
            return;
        }

        if (ent.Comp.DockedPod is not { } podUid
            || !TryComp<TransitPodComponent>(podUid, out var pod)
            || pod.State != TransitPodState.Open)
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-not-ready"), ent, args.Actor);
            UpdateDestinations(ent);
            return;
        }

        var target = GetEntity(args.Destination);
        if (target != null && !HasComp<TransitTubeStationComponent>(target))
        {
            UpdateDestinations(ent);
            return;
        }

        ReleaseReservation((podUid, pod));
        pod.Target = target;
        pod.AnyFree = target == null && args.AnyFree;
        pod.Plan.Clear();
        Dirty(podUid, pod);

        var text = target is { } targetUid && TryComp<TransitTubeStationComponent>(targetUid, out var targetStation)
            ? Loc.GetString("transit-pod-destination-set", ("destination", GetDestinationName((targetUid, targetStation)) ?? string.Empty))
            : Loc.GetString(pod.AnyFree ? "transit-pod-destination-any" : "transit-pod-destination-cleared");

        _popup.PopupEntity(text, ent, args.Actor);
        UpdateDestinations(ent);
    }

    #endregion

    #region Device ports

    [SubscribeLocalEvent]
    private void OnStationInit(Entity<TransitTubeStationComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSourcePorts(ent, ent.Comp.ArrivedPort, ent.Comp.DepartedPort);
        _deviceLink.EnsureSinkPorts(ent, ent.Comp.DepartPort, ent.Comp.HoldPort);
    }

    [SubscribeLocalEvent]
    private void OnStationSignal(Entity<TransitTubeStationComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port == ent.Comp.HoldPort)
        {
            ent.Comp.Holding = true;
            return;
        }

        if (args.Port != ent.Comp.DepartPort)
            return;

        ent.Comp.Holding = false;

        // Send the docked pod off now. A depot pod with nowhere to be leaves on its usual way.
        if (ent.Comp.DockedPod is { } podUid
            && TryComp<TransitPodComponent>(podUid, out var pod)
            && pod.State == TransitPodState.Open)
        {
            pod.NextDeparture = _timing.CurTime;

            if (ent.Comp.Depot && pod.Target == null)
            {
                pod.AnyFree = true;
                Dirty(podUid, pod);
            }
        }
    }

    #endregion

    private void PopupPassengers(Entity<TransitPodComponent> pod, string message)
    {
        foreach (var passenger in GetPassengers(pod))
            _popup.PopupEntity(Loc.GetString(message), passenger, passenger);
    }
}
