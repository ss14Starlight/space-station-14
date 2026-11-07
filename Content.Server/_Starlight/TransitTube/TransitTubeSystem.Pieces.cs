using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.Popups;

namespace Content.Server._Starlight.TransitTube;

/// <summary>
/// Special tube pieces: speed-ups and brakes, block signals, taggers and request stops.
/// Routing itself is shared, see <see cref="SharedTransitTubeSystem"/>.
/// </summary>
public sealed partial class TransitTubeSystem
{
    private static readonly TimeSpan _signalCheckInterval = TimeSpan.FromSeconds(0.25);

    private readonly HashSet<EntityUid> _section = [];

    protected override void OnPodRouted(Entity<TransitTubeComponent> tube, Entity<DisposalHolderComponent> holder, Entity<TransitPodComponent> pod, Direction next)
    {
        var rotation = Transform(tube).LocalRotation;
        var travel = holder.Comp.CurrentDirection;

        // A tagger gives the pod a new destination rather than piling up old ones.
        if (HasComp<DisposalTaggerComponent>(tube))
            _holder.ClearTags(holder);

        if (TryComp<TransitTubeSpeedComponent>(tube, out var speed) && travel != Direction.Invalid && SameWay(travel, speed.Direction, rotation))
            SetSpeed(pod, holder, speed.Speed);

        if (next != Direction.Invalid
            && TryComp<TransitTubeBlockSignalComponent>(tube, out var signal)
            && SameWay(next, signal.Direction, rotation)
            && SectionOccupied((tube, signal), next, pod))
        {
            pod.Comp.HeldAtSignal = tube;
            pod.Comp.NextSignalCheck = _timing.CurTime + _signalCheckInterval;
            _holder.SetHalted(holder, true);
        }
    }

    /// <summary>
    /// Sets the speed of a pod. Null puts it back to normal.
    /// </summary>
    public void SetSpeed(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder, float? speed)
    {
        var target = speed ?? pod.Comp.BaseSpeed;
        pod.Comp.Boosted = target > pod.Comp.BaseSpeed + 0.01f;
        _holder.SetTraversalSpeed(holder, target);
    }

    private void UpdateHeld(Entity<TransitPodComponent> pod, DisposalHolderComponent holder, TimeSpan now)
    {
        if (pod.Comp.HeldAtSignal is not { } signalUid || now < pod.Comp.NextSignalCheck)
            return;

        pod.Comp.NextSignalCheck = now + _signalCheckInterval;

        if (TryComp<TransitTubeBlockSignalComponent>(signalUid, out var signal)
            && SectionOccupied((signalUid, signal), holder.CurrentDirection, pod))
            return;

        pod.Comp.HeldAtSignal = null;
        _holder.SetHalted((pod, holder), false);
    }

    /// <summary>
    /// Whether another pod is in the section ahead of a block signal: every tube up to the next
    /// signal or station, which is included.
    /// </summary>
    private bool SectionOccupied(Entity<TransitTubeBlockSignalComponent> signal, Direction direction, EntityUid self)
    {
        _section.Clear();

        if (!TryComp<DisposalTubeComponent>(signal, out var signalTube))
            return false;

        Entity<DisposalTubeComponent> current = (signal, signalTube);
        for (var i = 0; i < signal.Comp.MaxSectionLength && direction != Direction.Invalid; i++)
        {
            if (_disposalTube.GetTubeInDirection(current, direction) is not { } nextUid
                || !TryComp<DisposalTubeComponent>(nextUid, out var nextTube)
                || !_section.Add(nextUid))
                break;

            if (HasComp<TransitTubeStationComponent>(nextUid)
                || HasComp<TransitTubeBlockSignalComponent>(nextUid)
                || !TryComp<TransitTubeComponent>(nextUid, out var transit))
                break;

            direction = PickExit((nextUid, transit), nextTube, direction);
            current = (nextUid, nextTube);
        }

        if (_section.Count == 0)
            return false;

        var query = EntityQueryEnumerator<TransitPodComponent, DisposalHolderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var holder, out var xform))
        {
            if (uid == self)
                continue;

            // The tube a pod sits on covers docked and stalled pods too, the next tube covers pods about to enter.
            if (holder.NextTube is { } nextTubeUid && _section.Contains(nextTubeUid)
                || TryGetTube(xform, out var under) && _section.Contains(under))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A boosted pod slams into a dead end: everyone inside gets hurt, and so does the tube,
    /// until it breaks open.
    /// </summary>
    private void Impact(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder)
    {
        HurtPassengers(pod, pod.Comp.ImpactDamage, 1f);
        _audio.PlayPvs(pod.Comp.CollisionSound, pod);

        if (holder.Comp.NextTube is { } wall && Exists(wall))
            _damageable.ChangeDamage(wall, pod.Comp.ImpactTubeDamage);
    }

    private bool TryRequestStop(Entity<TransitTubeStationComponent> station, EntityUid user)
    {
        if (!station.Comp.RequestStop || station.Comp.DockedPod != null)
            return false;

        station.Comp.StopRequested = true;
        _appearance.SetData(station, TransitStationVisuals.StopRequested, true);
        _popup.PopupEntity(Loc.GetString("transit-station-stop-requested"), station, user);
        return true;
    }

    private void RequestStopFromInside(Entity<TransitPodComponent> pod, EntityUid passenger)
    {
        if (pod.Comp.StopRequested)
        {
            _popup.PopupEntity(Loc.GetString("transit-pod-moving"), passenger, passenger);
            return;
        }

        pod.Comp.StopRequested = true;
        _popup.PopupEntity(Loc.GetString("transit-pod-stop-requested"), passenger, passenger, PopupType.Medium);
    }
}
