// ReSharper disable CheckNamespace

using Content.Shared.Cargo.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Pinpointer;

public sealed partial class StationMapSystem
{
    private EntityUid? GetMainStationGrid(EntityUid stationUid)
    {
        if (!TryComp<StationDataComponent>(stationUid, out var stationData))
            return null;

        foreach (var grid in stationData.MainGrids)
        {
            if (HasComp<MapGridComponent>(grid))
                return grid;
        }

        EntityUid? targetGrid = null;
        var largestGridSize = -1f;

        foreach (var member in stationData.Grids)
        {
            if (HasComp<TradeStationComponent>(member) ||
                !TryComp(member, out MapGridComponent? grid))
            {
                continue;
            }

            var gridSize = grid.LocalAABB.Size.LengthSquared();
            if (gridSize < largestGridSize)
                continue;

            targetGrid = member;
            largestGridSize = gridSize;
        }

        return targetGrid;
    }
}
