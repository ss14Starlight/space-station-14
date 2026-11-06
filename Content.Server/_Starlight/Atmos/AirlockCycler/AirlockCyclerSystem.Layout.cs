using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.DeviceLinking;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

public sealed partial class AirlockCyclerSystem
{
    private sealed class Layout
    {
        public readonly List<SideLayout> Sides = [];
        public readonly List<Entity<GasVentPumpComponent>> Vents = [];
        public float? ChamberPressure;
        public float? TankPressure;
    }

    private sealed class SideLayout
    {
        public readonly List<DoorLayout> Doors = [];
        public float? OutsidePressure;
        public int Buttons;
    }

    private readonly record struct DoorLayout(EntityUid Door, float? OutsidePressure);

    private readonly HashSet<Vector2i> _chamber = [];
    private readonly HashSet<Vector2i> _doorTiles = [];
    private readonly Queue<Vector2i> _frontier = new();

    private Layout BuildLayout(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source, TransformComponent xform)
    {
        var layout = new Layout();
        var gridUid = xform.GridUid;
        TryComp<MapGridComponent>(gridUid, out var grid);

        foreach (var linked in _deviceLink.GetLinkedSinks((ent, source), ent.Comp.VentPort))
        {
            if (_ventQuery.TryComp(linked, out var vent) && Transform(linked).GridUid == gridUid)
                layout.Vents.Add((linked, vent));
        }

        var sideDoors = new List<List<(EntityUid Door, Vector2i Tile)>>();
        _doorTiles.Clear();
        for (var i = 0; i < ent.Comp.Sides.Count; i++)
        {
            var side = new SideLayout();
            var doors = new List<(EntityUid, Vector2i)>();
            foreach (var linked in _deviceLink.GetLinkedSinks((ent, source), ent.Comp.Sides[i]))
            {
                if (_buttonQuery.HasComp(linked))
                {
                    side.Buttons++;
                    continue;
                }

                if (!_doorQuery.HasComp(linked))
                    continue;

                var doorXform = Transform(linked);
                if (grid == null || doorXform.GridUid != gridUid)
                {
                    side.Doors.Add(new DoorLayout(linked, null));
                    continue;
                }

                var tile = _map.LocalToTile(gridUid!.Value, grid, doorXform.Coordinates);
                doors.Add((linked, tile));
                _doorTiles.Add(tile);
            }

            layout.Sides.Add(side);
            sideDoors.Add(doors);
        }

        if (grid == null || gridUid == null)
            return layout;

        var mapUid = xform.MapUid;
        var gridEnt = gridUid.Value;

        // Chamber pressure and tank pressure from the vents.
        var chamberSum = 0f;
        var chamberCount = 0;
        _chamber.Clear();
        _frontier.Clear();
        foreach (var vent in layout.Vents)
        {
            var tile = _map.LocalToTile(gridEnt, grid, Transform(vent).Coordinates);
            if (_chamber.Add(tile))
                _frontier.Enqueue(tile);

            if (_atmos.GetTileMixture(gridEnt, mapUid, tile) is { } mix)
            {
                chamberSum += mix.Pressure;
                chamberCount++;
            }

            if (layout.TankPressure == null && _nodes.TryGetNode(vent.Owner, vent.Comp.Inlet, out PipeNode? pipe))
                layout.TankPressure = pipe.Air.Pressure;
        }

        if (chamberCount > 0)
            layout.ChamberPressure = chamberSum / chamberCount;

        // Flood fill the chamber.
        while (_frontier.TryDequeue(out var tile) && _chamber.Count < ent.Comp.MaxChamberTiles)
        {
            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var dir = (AtmosDirection)(1 << i);
                var next = tile.Offset(dir);
                if (_chamber.Contains(next) || _doorTiles.Contains(next))
                    continue;

                if (_atmos.IsTileAirBlockedCached(gridEnt, tile, dir)
                    || _atmos.IsTileAirBlockedCached(gridEnt, next, dir.GetOpposite())
                    || !_map.TryGetTileRef(gridEnt, grid, next, out var tileRef)
                    || tileRef.Tile.IsEmpty)
                    continue;

                _chamber.Add(next);
                _frontier.Enqueue(next);
            }
        }

        // Pressure behind each door.
        for (var i = 0; i < sideDoors.Count; i++)
        {
            var side = layout.Sides[i];
            var sum = 0f;
            var count = 0;
            foreach (var (door, tile) in sideDoors[i])
            {
                var pressure = GetOutsidePressure(gridEnt, mapUid, tile);
                side.Doors.Add(new DoorLayout(door, pressure));
                if (pressure is not { } p)
                    continue;

                sum += p;
                count++;
            }

            if (count > 0)
                side.OutsidePressure = sum / count;
        }

        return layout;
    }

    private float? GetOutsidePressure(EntityUid grid, EntityUid? map, Vector2i doorTile)
    {
        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var dir = (AtmosDirection)(1 << i);
            if (!_chamber.Contains(doorTile.Offset(dir)))
                continue;

            // Walk outwards across the door; skip a few air-blocked tiles (a second door, a firelock).
            var outward = dir.GetOpposite();
            var tile = doorTile;
            for (var step = 0; step < 3; step++)
            {
                tile = tile.Offset(outward);
                if (_atmos.IsTileSpace(grid, map, tile))
                    return _atmos.GetTileMixture(grid, map, tile)?.Pressure ?? 0f;

                if (_atmos.GetTileMixture(grid, map, tile) is { } mix)
                    return mix.Pressure;
            }

            return null;
        }

        return null;
    }
}
