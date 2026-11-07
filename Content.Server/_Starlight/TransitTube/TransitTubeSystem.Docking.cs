using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    private void Dock(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder, Entity<TransitTubeStationComponent> station, bool instant = false)
    {
        _holder.SetHalted(holder, true);

        var stationXform = Transform(station);
        _xform.SetCoordinates(pod, stationXform.Coordinates);

        if (holder.Comp.CurrentDirection != Direction.Invalid)
            pod.Comp.TravelDirection = holder.Comp.CurrentDirection;

        pod.Comp.Station = station;
        pod.Comp.Bounced = false;
        pod.Comp.StopRequested = false;
        station.Comp.StopRequested = false;
        _appearance.SetData(station, TransitStationVisuals.StopRequested, false);
        station.Comp.DockedPod = pod;
        Dirty(station);

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

        foreach (var passenger in GetPassengers(pod))
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-arrived"), passenger, passenger);
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

        // Carry on the way the pod was going, otherwise turn back (terminus).
        var exits = _disposalTube.GetTubeConnectableDirections((stationUid, tube))
            .OrderByDescending(exit => exit == pod.Comp.TravelDirection);

        foreach (var exit in exits)
        {
            if (!_holder.Launch(holder, (stationUid, tube), exit))
                continue;

            pod.Comp.Station = null;
            Undock((stationUid, station));
            SetState(pod, TransitPodState.Moving);
            _audio.PlayPvs(pod.Comp.DepartSound, pod);
            return;
        }

        // Nowhere to go, open up again and try later.
        SetState(pod, TransitPodState.Open);
        pod.Comp.NextDeparture = _timing.CurTime + station.DepartDelay;
        SetStationVisual((stationUid, station), TransitStationVisualState.Open);
    }

    private bool Bounce(Entity<DisposalHolderComponent> holder)
    {
        if (Exists(holder.Comp.CurrentTube) && Exists(holder.Comp.NextTube))
            return _holder.Reverse(holder);

        // The tube ahead is missing: leave the current tube by any other exit.
        if (holder.Comp.CurrentTube is not { } currentUid || !TryComp<DisposalTubeComponent>(currentUid, out var current))
            return false;

        foreach (var exit in _disposalTube.GetTubeConnectableDirections((currentUid, current)))
        {
            if (exit != holder.Comp.CurrentDirection && _holder.Launch(holder, (currentUid, current), exit))
                return true;
        }

        return false;
    }

    private void Stall(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        _holder.SetHalted(holder, true);
        SetState(pod, TransitPodState.Stalled);
        SetSpeed(pod, holder, null);
        pod.Comp.HeldAtSignal = null;

        // Nothing holds the pod up any more: everyone falls out.
        if (!TryGetTube(Transform(pod), out _))
            EjectAll(pod, Transform(pod).Coordinates);
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
