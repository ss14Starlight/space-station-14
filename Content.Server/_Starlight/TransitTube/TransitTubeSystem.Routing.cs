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
            if (TryFindRoute(station, pod, uid => uid == target, out _))
                return true;

            pod.Comp.Target = null;
            pod.Comp.Plan.Clear();
            PopupPassengers(pod, "transit-pod-no-route");
            return false;
        }

        if (!pod.Comp.AnyFree)
            return false;

        if (!TryFindRoute(station, pod, uid => IsFreeStation(uid, pod), out var goal)
            || !TryComp<TransitTubeStationComponent>(goal, out var goalStation))
            return false;

        pod.Comp.Target = goal;
        Reserve((goal, goalStation), pod);
        return true;
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
    private bool TryFindRoute(EntityUid origin, Entity<TransitPodComponent>? pod, Func<EntityUid, bool> isGoal, out EntityUid goal)
    {
        goal = default;
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
            var through = PickExit(ent, tube, travel);
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

    private void WritePlan(Entity<TransitPodComponent> pod, EntityUid origin, (EntityUid Tube, Direction Travel) goal)
    {
        pod.Comp.Plan.Clear();

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

        var query = EntityQueryEnumerator<TransitTubeStationComponent>();
        while (query.MoveNext(out var depotUid, out var depot))
        {
            if (!depot.Depot
                || depot.DockedPod is not { } podUid
                || !TryComp<TransitPodComponent>(podUid, out var pod)
                || pod.State != TransitPodState.Open
                || pod.Target != null)
                continue;

            if (!TryFindRoute(depotUid, (podUid, pod), uid => uid == station.Owner, out _))
                continue;

            pod.Target = station;
            Reserve(station, (podUid, pod));
            pod.NextDeparture = _timing.CurTime;
            _popup.PopupEntity(Loc.GetString("transit-station-pod-called"), station, user);
            return true;
        }

        return false;
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
        var destinations = new List<TransitDestinationEntry>();
        var query = EntityQueryEnumerator<TransitTubeStationComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == station.Owner || other.Depot || GetDestinationName((uid, other)) is not { } name)
                continue;

            if (TryFindRoute(station, null, candidate => candidate == uid, out _))
                destinations.Add(new TransitDestinationEntry(GetNetEntity(uid), name));
        }

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
        if (!string.IsNullOrWhiteSpace(station.Comp.Destination))
            return station.Comp.Destination;

        return CompOrNull<LabelComponent>(station)?.CurrentLabel is { Length: > 0 } label ? label : null;
    }

    [SubscribeLocalEvent]
    private void OnSelectDestination(Entity<TransitTubeStationComponent> ent, ref TransitStationSelectDestinationMessage args)
    {
        if (ent.Comp.DockedPod is not { } podUid
            || !TryComp<TransitPodComponent>(podUid, out var pod)
            || pod.State != TransitPodState.Open)
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-not-ready"), ent, args.Actor);
            return;
        }

        var target = GetEntity(args.Destination);
        if (target != null && !HasComp<TransitTubeStationComponent>(target))
            return;

        pod.Target = target;
        pod.AnyFree = target == null && args.AnyFree;
        pod.Plan.Clear();

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
                pod.AnyFree = true;
        }
    }

    #endregion

    private void PopupPassengers(Entity<TransitPodComponent> pod, string message)
    {
        foreach (var passenger in GetPassengers(pod))
            _popup.PopupEntity(Loc.GetString(message), passenger, passenger);
    }
}
