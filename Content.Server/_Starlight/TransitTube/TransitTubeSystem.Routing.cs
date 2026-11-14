using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.Labels.Components;
using Content.Shared.Verbs;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    /// <summary>
    /// How many (tube, direction) states a route search may look at before it gives up.
    /// </summary>
    private const int MaxRouteSearch = 4096;

    /// <summary>
    /// What leaving the way a plain junction sends pods by default costs a planned route, in tubes.
    /// Bigger than any search, so a pod keeps to the line its builders laid out whenever that gets
    /// it there, and only takes other branches when it has to.
    /// </summary>
    private const int DetourCost = MaxRouteSearch * 4;

    private readonly Dictionary<(EntityUid Tube, Direction Travel), (EntityUid Tube, Direction Travel, Direction Exit)> _cameFrom = [];
    private readonly Dictionary<(EntityUid Tube, Direction Travel), int> _routeCost = [];
    private readonly Dictionary<(EntityUid Tube, Direction Travel), string?> _routeTag = [];
    private readonly HashSet<(EntityUid Tube, Direction Travel)> _routeClosed = [];
    private readonly PriorityQueue<(EntityUid Tube, Direction Travel), int> _frontier = new();
    private readonly List<(Direction Exit, int Cost)> _choices = [];

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
        RefreshPodDestinations(pod);
    }

    private bool TryGetRouteStart(Entity<TransitPodComponent> pod, out EntityUid origin, out (EntityUid Tube, Direction Travel)? start)
    {
        start = null;
        origin = default;

        if (pod.Comp.Station is { } station)
        {
            origin = station;
            return true;
        }

        if (!TryComp<DisposalHolderComponent>(pod, out var holder)
            || holder.CurrentTube is not { } current
            || holder.NextTube is not { } next
            || holder.CurrentDirection == Direction.Invalid)
            return false;

        origin = current;
        start = (next, holder.CurrentDirection);
        return true;
    }

    private bool IsFreeStation(EntityUid uid, EntityUid pod)
        => TryComp<TransitTubeStationComponent>(uid, out var station)
        && !station.Depot
        && CanDockAt((uid, station), pod);

    /// <summary>
    /// Searches over (tube, direction of travel) from a station to the nearest tube matching
    /// <paramref name="isGoal"/>, taking only the turns a pod can actually make. Pieces that pick the
    /// way by themselves (switches, side branches, routers, one-way tubes) are followed as they are set
    /// now, and plain junctions are left only when the way they send pods by default doesn't get there.
    /// </summary>
    /// <param name="pod">The pod to plan for: its tags steer routers. Null plans for a pod without tags.</param>
    /// <param name="length">How many tubes the found route runs through.</param>
    /// <param name="start">For a pod on the move: the tube it is heading into and its direction of travel.
    /// The search then only goes on from there, the way the pod already goes. Null searches every way out of
    /// <paramref name="origin"/>, like a pod leaving a station.</param>
    /// <param name="writePlan">Give the found route to <paramref name="pod"/> as its plan.
    /// False only checks that there is one.</param>
    private bool TryFindRoute(EntityUid origin, Entity<TransitPodComponent>? pod, Func<EntityUid, bool> isGoal, out EntityUid goal, out int length,
        (EntityUid Tube, Direction Travel)? start = null, bool writePlan = true)
    {
        goal = default;
        length = 0;
        _cameFrom.Clear();
        _routeCost.Clear();
        _routeTag.Clear();
        _routeClosed.Clear();
        _frontier.Clear();

        if (!TryComp<DisposalTubeComponent>(origin, out var originTube))
            return false;

        Entity<DisposalHolderComponent>? holder = pod is { } podUid && TryComp<DisposalHolderComponent>(podUid, out var holderComp)
            ? (podUid.Owner, holderComp)
            : null;

        if (start is { } moving)
        {
            Relax(moving, (origin, Direction.Invalid, moving.Travel), 1, null);
        }
        else
        {
            foreach (var exit in _disposalTube.GetTubeConnectableDirections((origin, originTube)))
            {
                if (_disposalTube.GetTubeInDirection((origin, originTube), exit) is { } next)
                    Relax((next, exit), (origin, Direction.Invalid, exit), 1, null);
            }
        }

        var searched = 0;
        while (searched < MaxRouteSearch && _frontier.TryDequeue(out var state, out var cost))
        {
            if (!_routeClosed.Add(state))
                continue;

            searched++;

            if (state.Tube != origin && isGoal(state.Tube))
            {
                goal = state.Tube;
                length = RouteLength(origin, state);
                if (writePlan && pod is { } podEnt)
                    WritePlan(podEnt, origin, state);
                return true;
            }

            if (!TryComp<DisposalTubeComponent>(state.Tube, out var tube))
                continue;

            // A tagger on the way swaps the pod's tags, and the routers past it go by the new one.
            var tag = TryComp<DisposalTaggerComponent>(state.Tube, out var tagger) ? tagger.Tag : _routeTag.GetValueOrDefault(state);

            GetChoices(state.Tube, tube, state.Travel, holder, tag, _choices);
            foreach (var (exit, stepCost) in _choices)
            {
                if (_disposalTube.GetTubeInDirection((state.Tube, tube), exit) is { } next)
                    Relax((next, exit), (state.Tube, state.Travel, exit), cost + stepCost, tag);
            }
        }

        return false;
    }

    private void Relax((EntityUid Tube, Direction Travel) state, (EntityUid Tube, Direction Travel, Direction Exit) from, int cost, string? tag)
    {
        if (_routeClosed.Contains(state) || (_routeCost.TryGetValue(state, out var known) && known <= cost))
            return;

        _routeCost[state] = cost;
        _cameFrom[state] = from;
        _routeTag[state] = tag;
        _frontier.Enqueue(state, cost);
    }

    /// <summary>
    /// The ways a pod entering a tube going <paramref name="travel"/> can leave it, with what each costs.
    /// </summary>
    private void GetChoices(EntityUid uid, DisposalTubeComponent tube, Direction travel, Entity<DisposalHolderComponent>? holder,
        string? tag, List<(Direction Exit, int Cost)> choices)
    {
        choices.Clear();

        if (!TryComp<TransitTubeComponent>(uid, out var transit))
            return;

        Entity<TransitTubeComponent> ent = (uid, transit);

        if (HasComp<TransitTubeStationComponent>(uid))
        {
            AddChoiceOrTurnBack(ent, tube, travel, PickExit((uid, transit, tube), travel), choices);
            return;
        }

        if (FixedExit(ent, tube, holder, travel, tag) is { } fixedExit)
        {
            AddChoiceOrTurnBack(ent, tube, travel, fixedExit, choices);
            return;
        }

        var usual = PickExit((uid, transit, tube), travel);
        var back = travel.GetOpposite();
        foreach (var exit in _disposalTube.GetTubeConnectableDirections((uid, tube)))
        {
            if (exit == back)
                continue;

            if (Math.Abs(Angle.ShortestDistance(travel.ToAngle(), exit.ToAngle()).Degrees) > transit.MaxTurn + 1)
                continue;

            choices.Add((exit, exit == usual ? 1 : 1 + DetourCost));
        }

        if (choices.Count == 0)
            AddChoiceOrTurnBack(ent, tube, travel, Direction.Invalid, choices);
    }

    /// <summary>
    /// Adds a way on, or, at a dead end, the way back: a pod turns around there.
    /// </summary>
    private void AddChoiceOrTurnBack(Entity<TransitTubeComponent> ent, DisposalTubeComponent tube, Direction travel, Direction exit,
        List<(Direction Exit, int Cost)> choices)
    {
        if (exit != Direction.Invalid)
        {
            choices.Add((exit, 1));
            return;
        }

        var back = travel.GetOpposite();
        if (travel != Direction.Invalid && HasExit(ent, tube, back))
            choices.Add((back, 1));
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

        if (!_access.IsAllowed(user, station))
        {
            _popup.PopupEntity(Loc.GetString("transit-station-access-denied"), station, user);
            return true;
        }

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

            if (!TryFindRoute(depotUid, (podUid, pod), uid => uid == station.Owner, out _, out var length, writePlan: false) || length >= bestLength)
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

    private List<TransitDestinationEntry> CollectDestinations(EntityUid origin, Entity<TransitPodComponent>? pod, (EntityUid Tube, Direction Travel)? start)
    {
        var destinations = new List<TransitDestinationEntry>();
        var found = new HashSet<EntityUid>();
        TryFindRoute(origin, pod, uid =>
        {
            if (uid != origin
                && TryComp<TransitTubeStationComponent>(uid, out var other)
                && !other.Depot
                && GetDestinationName((uid, other)) is { } name
                && found.Add(uid))
                destinations.Add(new TransitDestinationEntry(GetNetEntity(uid), name));

            return false;
        }, out _, out _, start, writePlan: false);

        destinations.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return destinations;
    }

    private void UpdateDestinations(Entity<TransitTubeStationComponent> station)
    {
        TransitPodComponent? pod = null;
        Entity<TransitPodComponent>? podEnt = null;
        if (station.Comp.DockedPod is { } podUid && TryComp(podUid, out pod))
            podEnt = (podUid, pod);

        var destinations = CollectDestinations(station, podEnt, null);

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
        SelectAtStation(ent, args.Actor, args.Destination, args.AnyFree);
        UpdateDestinations(ent);
    }

    private void SelectAtStation(Entity<TransitTubeStationComponent> station, EntityUid actor, NetEntity? destination, bool anyFree)
    {
        if (!_access.IsAllowed(actor, station))
        {
            _popup.PopupEntity(Loc.GetString("transit-station-access-denied"), station, actor);
            return;
        }

        if (station.Comp.DockedPod is not { } podUid
            || !TryComp<TransitPodComponent>(podUid, out var pod)
            || pod.State != TransitPodState.Open)
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-not-ready"), station, actor);
            return;
        }

        var target = GetEntity(destination);
        if (target != null && !HasComp<TransitTubeStationComponent>(target))
            return;

        Entity<TransitPodComponent> podEnt = (podUid, pod);
        ReleaseReservation(podEnt);
        pod.Target = target;
        pod.AnyFree = target == null && anyFree;
        pod.Plan.Clear();
        Dirty(podEnt);

        PopupDestination(podEnt, actor);
        RefreshPodDestinations(podEnt);
    }

    private void PopupDestination(Entity<TransitPodComponent> pod, EntityUid actor)
    {
        var text = pod.Comp.Target is { } targetUid && TryComp<TransitTubeStationComponent>(targetUid, out var targetStation)
            ? Loc.GetString("transit-pod-destination-set", ("destination", GetDestinationName((targetUid, targetStation)) ?? string.Empty))
            : Loc.GetString(pod.Comp.AnyFree ? "transit-pod-destination-any" : "transit-pod-destination-cleared");

        _popup.PopupEntity(text, actor, actor);
    }

    [SubscribeLocalEvent]
    private void OnPodVerbs(Entity<TransitPodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        // Passengers pick where to go from inside, docked or on the way.
        if (ent.Comp.Cargo || !IsPassenger(ent, args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("transit-station-verb-destination"),
            Act = () => OpenPodDestinations(ent, user),
        });
    }

    private bool IsPassenger(Entity<TransitPodComponent> pod, EntityUid uid)
        => GetPassengers(pod).Contains(uid);

    private bool OpenPodDestinations(Entity<TransitPodComponent> pod, EntityUid user)
    {
        if (UpdatePodDestinations(pod) == 0)
        {
            _popup.PopupEntity(Loc.GetString("transit-station-ui-empty"), user, user);
            return false;
        }

        _ui.OpenUi(pod.Owner, TransitPodUiKey.Key, user);
        return true;
    }

    /// <returns>How many destinations the pod can pick from.</returns>
    private int UpdatePodDestinations(Entity<TransitPodComponent> pod)
    {
        var destinations = TryGetRouteStart(pod, out var origin, out var start)
            ? CollectDestinations(origin, pod, start)
            : [];

        var state = new TransitStationBoundUserInterfaceState(destinations, GetNetEntity(pod.Comp.Target), pod.Comp.AnyFree);
        _ui.SetUiState(pod.Owner, TransitPodUiKey.Key, state);
        return destinations.Count;
    }

    private void RefreshPodDestinations(Entity<TransitPodComponent> pod)
    {
        if (_ui.IsUiOpen(pod.Owner, TransitPodUiKey.Key))
            UpdatePodDestinations(pod);
    }

    [SubscribeLocalEvent]
    private void OnPodSelectDestination(Entity<TransitPodComponent> ent, ref TransitStationSelectDestinationMessage args)
    {
        if (!IsPassenger(ent, args.Actor))
            return;

        if (ent.Comp.Station is { } stationUid && TryComp<TransitTubeStationComponent>(stationUid, out var station))
            SelectAtStation((stationUid, station), args.Actor, args.Destination, args.AnyFree);
        else
            SelectOnTheWay(ent, args.Actor, GetEntity(args.Destination), args.AnyFree);

        UpdatePodDestinations(ent);
    }

    private void SelectOnTheWay(Entity<TransitPodComponent> pod, EntityUid actor, EntityUid? target, bool anyFree)
    {
        if (target != null && !HasComp<TransitTubeStationComponent>(target)
            || pod.Comp.State != TransitPodState.Moving
            || !TryGetRouteStart(pod, out var origin, out var start))
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-not-ready"), actor, actor);
            return;
        }

        // Check there is a way before giving up the old trip.
        if (target is { } goal && !TryFindRoute(origin, pod, uid => uid == goal, out _, out _, start, writePlan: false))
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-no-route"), actor, actor);
            return;
        }

        ReleaseReservation(pod);
        pod.Comp.Plan.Clear();
        pod.Comp.Target = null;
        pod.Comp.AnyFree = false;

        // Going somewhere now, so the pod no longer means to stop at the next station.
        pod.Comp.StopRequested = false;

        if (target is { } chosen)
        {
            TryFindRoute(origin, pod, uid => uid == chosen, out _, out _, start);
            pod.Comp.Target = chosen;
        }
        else if (anyFree)
        {
            // Head for the nearest free station, or look again at the next stop.
            if (TryFindRoute(origin, pod, uid => IsFreeStation(uid, pod), out var free, out _, start)
                && TryComp<TransitTubeStationComponent>(free, out var freeStation))
            {
                pod.Comp.Target = free;
                Reserve((free, freeStation), pod);
            }

            pod.Comp.AnyFree = true;
        }

        Dirty(pod);
        PopupDestination(pod, actor);
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
