// ReSharper disable CheckNamespace

using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Station.Components;

namespace Content.Server.Salvage;

public sealed partial class SalvageSystem
{
    private void InitializeSurfaceReturn() =>
        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key,
            subs => subs.Event<ShuttleConsoleSurfaceReturnMessage>(OnSurfaceReturn));

    private void OnSurfaceReturn(Entity<ShuttleConsoleComponent> entity, ref ShuttleConsoleSurfaceReturnMessage args)
    {
        if (!TryComp<PilotComponent>(args.Actor, out var pilot) ||
            pilot.Console != entity.Owner)
        {
            return;
        }

        var consoleUid = _shuttleConsoles.GetDroneConsole(entity.Owner);
        if (consoleUid is not { } console ||
            !TryComp(console, out TransformComponent? consoleXform) ||
            consoleXform.GridUid is not { } shuttleUid ||
            !TryComp<ShuttleComponent>(shuttleUid, out var shuttle) ||
            HasComp<FTLComponent>(shuttleUid) ||
            !TryComp<SalvageExpeditionComponent>(consoleXform.MapUid, out var expedition))
        {
            return;
        }

        if (!_shuttle.CanFTL(shuttleUid, out _))
            return;

        ReturnShuttleToStation(shuttleUid, shuttle, expedition.Station);
    }

    private void ReturnShuttleToStation(
        EntityUid shuttleUid,
        ShuttleComponent shuttle,
        EntityUid stationUid,
        float? startupTime = null)
    {
        if (!TryComp(stationUid, out StationDataComponent? stationData))
        {
            Log.Warning($"Cannot return salvage shuttle {ToPrettyString(shuttleUid)}: {ToPrettyString(stationUid)} has no station data.");
            return;
        }

        EntityUid? targetGrid = null;
        foreach (var member in stationData.Grids)
        {
            if (!HasComp<TradeStationComponent>(member))
                continue;

            targetGrid = member;
            break;
        }

        if (targetGrid is null)
        {
            var largestGrid = _station.GetLargestGrid((stationUid, stationData));
            if (largestGrid is null)
            {
                Log.Warning($"No valid station grid found for salvage shuttle {ToPrettyString(shuttleUid)} returning to {ToPrettyString(stationUid)}.");
                return;
            }

            targetGrid = largestGrid;
            Log.Warning($"No trade station grid found for salvage shuttle {ToPrettyString(shuttleUid)} returning to {ToPrettyString(stationUid)}; using largest station grid {ToPrettyString(largestGrid.Value)}.");
        }

        _shuttle.FTLToDock(shuttleUid, shuttle, targetGrid.Value, startupTime);
    }
}
