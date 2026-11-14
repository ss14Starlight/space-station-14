using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    private bool TryGetTube(TransformComponent xform, out EntityUid tube, DisposalHolderComponent? holder = null)
    {
        tube = default;

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var found = false;
        var anchored = _map.GetAnchoredEntities(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
        while (anchored.MoveNext(out var uid))
        {
            if (!HasComp<TransitTubeComponent>(uid) || !HasComp<DisposalTubeComponent>(uid))
                continue;

            if (holder != null && (uid == holder.CurrentTube || uid == holder.NextTube))
            {
                tube = uid.Value;
                return true;
            }

            if (found)
                continue;

            tube = uid.Value;
            found = true;
        }

        return found;
    }

    private bool TryGetStation(TransitPodComponent pod, out Entity<TransitTubeStationComponent> station)
    {
        station = default;

        if (pod.Station is not { } uid || !TryComp<TransitTubeStationComponent>(uid, out var comp))
            return false;

        station = (uid, comp);
        return true;
    }

    private void SetState(Entity<TransitPodComponent> pod, TransitPodState state, TimeSpan? duration = null)
    {
        pod.Comp.State = state;
        pod.Comp.StateEnd = _timing.CurTime + (duration ?? TimeSpan.Zero);
        Dirty(pod);
    }

    private void SetStationVisual(Entity<TransitTubeStationComponent> station, TransitStationVisualState state)
        => _appearance.SetData(station, TransitStationVisuals.State, state);

    private void UpdatePodVisuals(Entity<TransitPodComponent> pod)
    {
        var count = GetPassengers(pod).Count();
        var load = count == 0 ? TransitPodLoad.Empty
            : count >= pod.Comp.MaxPassengers ? TransitPodLoad.Full
            : TransitPodLoad.Occupied;

        _appearance.SetData(pod, TransitPodVisuals.Occupied, count > 0);
        _appearance.SetData(pod, TransitPodVisuals.Load, load);
    }
}
