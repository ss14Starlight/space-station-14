using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    private void Dock(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder, Entity<TransitTubeStationComponent> station, bool instant = false)
    {
        _holder.SetHalted(holder.AsNullable(), true);

        var stationXform = Transform(station);
        _xform.SetCoordinates(pod, stationXform.Coordinates);

        if (holder.Comp.CurrentDirection != Direction.Invalid)
            pod.Comp.TravelDirection = holder.Comp.CurrentDirection;

        pod.Comp.Station = station;
        pod.Comp.Bounced = false;
        pod.Comp.Target = null;
        pod.Comp.AnyFree = false;
        pod.Comp.Plan.Clear();
        ReleaseReservation(pod);
        _deviceLink.InvokePort(station, station.Comp.ArrivedPort);
        pod.Comp.StopRequested = false;
        Dirty(pod);
        station.Comp.StopRequested = false;
        _appearance.SetData(station, TransitStationVisuals.StopRequested, false);
        station.Comp.DockedPod = pod;
        Dirty(station);
        RefreshPodDestinations(pod);

        if (instant)
        {
            OpenPod(pod, station);
            return;
        }

        SetState(pod, TransitPodState.Opening, station.Comp.OpenTime);
        SetStationVisual(station, TransitStationVisualState.Opening);
        _audio.PlayPvs(station.Comp.OpenSound, station);
    }

    private void OpenPod(Entity<TransitPodComponent> pod, Entity<TransitTubeStationComponent> station)
    {
        SetState(pod, TransitPodState.Open);
        pod.Comp.NextDeparture = _timing.CurTime + station.Comp.DepartDelay;
        SetStationVisual(station, TransitStationVisualState.Open);

        var text = GetDestinationName(station) is { } name
            ? Loc.GetString("transit-pod-arrived-at", ("station", name))
            : Loc.GetString("transit-pod-arrived");

        foreach (var passenger in GetPassengers(pod))
        {
            _popup.PopupEntity(text, passenger, passenger);
        }
    }

    private void Undock(Entity<TransitTubeStationComponent> station)
    {
        station.Comp.DockedPod = null;
        Dirty(station);
        SetStationVisual(station, TransitStationVisualState.Closed);
    }

    private void Depart(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        if (pod.Comp.Station is not { } stationUid
            || !TryComp<TransitTubeStationComponent>(stationUid, out var station)
            || !TryComp<DisposalTubeComponent>(stationUid, out var tube))
        {
            Stall(pod, holder);
            return;
        }

        Entity<TransitTubeStationComponent> stationEnt = (stationUid, station);

        // Held while the doors were closing: open up again and wait for the hold to clear.
        if (station.Holding)
        {
            Reopen(pod, stationEnt);
            return;
        }

        // A pod with a plan leaves the way it says. If that way is gone, it plans again, and failing
        // that gives the trip up, rather than heading off the wrong way past stations it could use.
        if (pod.Comp.Plan.TryGetValue((stationUid, Direction.Invalid), out var planned))
        {
            if (TryLaunchFrom(pod, holder, stationEnt, tube, planned))
                return;

            if (TryPlanTrip(pod, stationEnt)
                && pod.Comp.Plan.TryGetValue((stationUid, Direction.Invalid), out var replanned)
                && TryLaunchFrom(pod, holder, stationEnt, tube, replanned))
                return;

            if (pod.Comp.Target != null || pod.Comp.AnyFree)
            {
                CancelTrip(pod);
                PopupPassengers(pod, "transit-pod-no-route");
            }
        }

        // Carry on the way the pod was going, otherwise turn back (terminus).
        var exits = _disposalTube.GetTubeConnectableDirections((stationUid, tube))
            .OrderByDescending(exit => exit == pod.Comp.TravelDirection);

        foreach (var exit in exits)
        {
            if (TryLaunchFrom(pod, holder, stationEnt, tube, exit))
                return;
        }

        // Nowhere to go, open up again and try later.
        Reopen(pod, stationEnt);
    }

    private bool TryLaunchFrom(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder,
        Entity<TransitTubeStationComponent> station, DisposalTubeComponent tube, Direction exit)
    {
        if (!_holder.Launch(holder.AsNullable(), (station, tube), exit))
            return false;

        pod.Comp.Station = null;
        Undock(station);
        _deviceLink.InvokePort(station, station.Comp.DepartedPort);
        SetState(pod, TransitPodState.Moving);
        _audio.PlayPvs(pod.Comp.DepartSound, pod);
        return true;
    }

    private void Reopen(Entity<TransitPodComponent> pod, Entity<TransitTubeStationComponent> station)
    {
        SetState(pod, TransitPodState.Open);
        pod.Comp.NextDeparture = _timing.CurTime + station.Comp.DepartDelay;
        SetStationVisual(station, TransitStationVisualState.Open);
    }

    private bool Bounce(Entity<DisposalHolderComponent> holder)
    {
        if (Exists(holder.Comp.CurrentTube) && Exists(holder.Comp.NextTube))
            return _holder.Reverse(holder.AsNullable());

        // The tube ahead is missing: leave the current tube by any other exit.
        if (holder.Comp.CurrentTube is not { } currentUid || !TryComp<DisposalTubeComponent>(currentUid, out var current))
            return false;

        foreach (var exit in _disposalTube.GetTubeConnectableDirections((currentUid, current)))
        {
            if (exit != holder.Comp.CurrentDirection && _holder.Launch(holder.AsNullable(), (currentUid, current), exit))
                return true;
        }

        return false;
    }

    private void Stall(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        _holder.SetHalted(holder.AsNullable(), true);
        SetState(pod, TransitPodState.Stalled);
        SetSpeed(pod.AsNullable(), holder.AsNullable(), null);
        LeaveSignal(pod);
        pod.Comp.WaitingFor = null;
        pod.Comp.WaitingBehind = null;
        pod.Comp.Braked = false;
        ReleaseReservation(pod);

        // Nothing holds the pod up any more: everyone falls out.
        if (!TryGetTube(Transform(pod), out _, holder))
            EjectAll(pod, Transform(pod).Coordinates);
    }

    private void ReleaseHold(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        if (pod.Comp.HeldAtSignal == null && pod.Comp.WaitingFor == null && pod.Comp.WaitingBehind == null && !pod.Comp.Braked)
            return;

        LeaveSignal(pod);
        pod.Comp.WaitingFor = null;
        pod.Comp.WaitingBehind = null;
        pod.Comp.Braked = false;
        _holder.SetHalted(holder.AsNullable(), false);
    }

    private void ReleaseReservation(Entity<TransitPodComponent> pod)
    {
        if (pod.Comp.Reserved is { } stationUid
            && TryComp<TransitTubeStationComponent>(stationUid, out var station)
            && station.ReservedBy == pod.Owner)
            station.ReservedBy = null;

        pod.Comp.Reserved = null;
    }

    /// <summary>
    /// A boosted pod ran out of tube: it stops where it is and its passengers fly on.
    /// </summary>
    private void Derail(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        var travel = holder.Comp.CurrentDirection;
        var direction = travel.ToVec();
        var speed = holder.Comp.TraversalSpeed;
        var passengers = GetPassengers(pod).ToArray();

        Stall(pod, holder);
        EjectAll(pod, Transform(pod).Coordinates);

        foreach (var passenger in passengers)
        {
            _throwing.TryThrow(passenger, direction * 4f, speed);
        }
    }
}
