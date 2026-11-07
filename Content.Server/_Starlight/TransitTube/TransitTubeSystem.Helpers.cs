using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Tube;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    private bool TryGetTube(TransformComponent xform, out EntityUid tube)
    {
        tube = default;

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var anchored = _map.GetAnchoredEntities(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
        while (anchored.MoveNext(out var uid))
        {
            if (!HasComp<TransitTubeComponent>(uid) || !HasComp<DisposalTubeComponent>(uid))
                continue;

            tube = uid.Value;
            return true;
        }

        return false;
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
    }

    private void SetStationVisual(Entity<TransitTubeStationComponent> station, TransitStationVisualState state)
        => _appearance.SetData(station, TransitStationVisuals.State, state);

    private void UpdatePodVisuals(Entity<TransitPodComponent> pod)
        => _appearance.SetData(pod, TransitPodVisuals.Occupied, GetPassengers(pod).Any());
}
