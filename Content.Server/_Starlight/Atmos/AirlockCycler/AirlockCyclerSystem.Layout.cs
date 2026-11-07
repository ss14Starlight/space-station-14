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

        /// <summary>Average over every chamber tile, for display and for picking the pump direction.</summary>
        public float? ChamberPressure;

        /// <summary>Lowest and highest tile pressure in the chamber: the cycle is done only when both match.</summary>
        public float ChamberMin;
        public float ChamberMax;

        /// <summary>The flood fill hit <see cref="AirlockCyclerComponent.MaxChamberTiles"/>: the chamber is breached.</summary>
        public bool ChamberLeaks;

        public float? TankPressure;
    }

    private sealed class SideLayout
    {
        public readonly List<DoorLayout> Doors = [];

        /// <summary>Average behind the doors, for display only. Cycling uses the min/max.</summary>
        public float? OutsidePressure;
        public float OutsideMin = float.MaxValue;
        public float OutsideMax = float.MinValue;

        /// <summary>Some door of this side has no reading (not next to the chamber, or only walls behind it).</summary>
        public bool MissingReading;

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

        foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.VentPort))
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
            foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.Sides[i]))
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
                    side.MissingReading = true;
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

        // The chamber starts at the vents; the tank is the first vent's pipe net.
        _chamber.Clear();
        _frontier.Clear();
        foreach (var vent in layout.Vents)
        {
            var tile = _map.LocalToTile(gridEnt, grid, Transform(vent).Coordinates);
            if (_chamber.Add(tile))
                _frontier.Enqueue(tile);

            if (layout.TankPressure == null && _nodes.TryGetNode(vent.Owner, vent.Comp.Inlet, out PipeNode? pipe))
                layout.TankPressure = pipe.Air.Pressure;
        }

        // Flood fill the chamber.
        while (_frontier.TryDequeue(out var tile))
        {
            if (_chamber.Count >= ent.Comp.MaxChamberTiles)
            {
                layout.ChamberLeaks = true;
                break;
            }

            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var dir = (AtmosDirection)(1 << i);
                var next = tile.Offset(dir);
                if (_chamber.Contains(next) || _doorTiles.Contains(next))
                    continue;

                if (_atmos.IsTileAirBlockedCached(gridEnt, tile, dir)
                    || _atmos.IsTileAirBlockedCached(gridEnt, next, dir.GetOpposite()))
                    continue;

                // Open to space (no tile, or a space tile): the chamber can never hold pressure.
                if (!_map.TryGetTileRef(gridEnt, grid, next, out var tileRef)
                    || tileRef.Tile.IsEmpty
                    || _atmos.IsTileSpace(gridEnt, mapUid, next))
                {
                    layout.ChamberLeaks = true;
                    continue;
                }

                _chamber.Add(next);
                _frontier.Enqueue(next);
            }
        }

        // Chamber pressure over every tile, not just the vents': the vent tile settles first, and opening on it
        // alone lets the rest of the chamber blow out (or in).
        var chamberSum = 0f;
        var chamberCount = 0;
        layout.ChamberMin = float.MaxValue;
        layout.ChamberMax = float.MinValue;
        foreach (var tile in _chamber)
        {
            if (_atmos.GetTileMixture(gridEnt, mapUid, tile) is not { } mix)
                continue;

            var pressure = mix.Pressure;
            chamberSum += pressure;
            chamberCount++;
            layout.ChamberMin = MathF.Min(layout.ChamberMin, pressure);
            layout.ChamberMax = MathF.Max(layout.ChamberMax, pressure);
        }

        if (chamberCount > 0)
            layout.ChamberPressure = chamberSum / chamberCount;

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
                {
                    side.MissingReading = true;
                    continue;
                }

                sum += p;
                count++;
                side.OutsideMin = MathF.Min(side.OutsideMin, p);
                side.OutsideMax = MathF.Max(side.OutsideMax, p);
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

            // Walk outwards across the door; skip a few air-blocked tiles (a second door, a firelock). A closed
            // airlock keeps the mixture it had on its own tile, so a blocked tile's air is never what lies beyond.
            var outward = dir.GetOpposite();
            var tile = doorTile;
            for (var step = 0; step < 3; step++)
            {
                tile = tile.Offset(outward);
                if (_atmos.IsTileAirBlockedCached(grid, tile, outward.GetOpposite()))
                    continue;

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
