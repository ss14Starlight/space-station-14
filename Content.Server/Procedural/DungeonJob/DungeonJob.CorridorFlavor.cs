using System.Linq;
using System.Threading.Tasks;
using Content.Shared.Doors.Components;
using Content.Shared.Procedural;
using Content.Shared.Procedural.PostGeneration;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Procedural.DungeonJob;

public sealed partial class DungeonJob
{
    private static readonly ProtoId<TagPrototype> _corridorWindowTag = "Window";

    /// <summary>
    /// <see cref="CorridorFlavorDunGen"/>
    /// </summary>
    private async Task PostGen(
        CorridorFlavorDunGen gen,
        List<Dungeon> dungeons,
        HashSet<Vector2i> reservedTiles,
        IRobustRandom random)
    {
        if (gen.Length <= 0)
        {
            _sawmill.Error($"Corridor flavor line length wasn't positive, was {gen.Length}.");
            return;
        }

        if (gen.Prob < 0f || gen.Prob > 1f)
        {
            _sawmill.Error($"Corridor flavor probability not between 0 and 1, was {gen.Prob}.");
            return;
        }

        var contents = _prototype.Index(gen.Contents);
        // Directions, for turning the object away from the wall.
        var wallDirections = new[] { Direction.South, Direction.East, Direction.North, Direction.West };

        // Make sure there is a wall
        bool HasWallOrWindow(Vector2i tile)
        {
            var anchored = _maps.GetAnchoredEntities(_gridUid, _grid, tile);
            while (anchored.MoveNext(out var uid))
            {
                if (_tags.HasTag(uid.Value, WallTag) || _tags.HasTag(uid.Value, _corridorWindowTag))
                    return true;
            }

            return false;
        }

        // Don't block airlocks
        bool HasAirlock(Vector2i tile)
        {
            var anchored = _maps.GetAnchoredEntities(_gridUid, _grid, tile);
            while (anchored.MoveNext(out var uid))
            {
                if (_entManager.HasComponent<AirlockComponent>(uid.Value))
                    return true;
            }

            return false;
        }

        bool HasAirlockNearby(Vector2i tile)
        {
            if (HasAirlock(tile))
                return true;

            return wallDirections.Any(direction => HasAirlock(tile + direction.ToIntVec()));
        }

        foreach (var dungeon in dungeons)
        {
            bool IsValidCorridorTile(Vector2i tile, Direction wallDirection)
            {
                var inward = wallDirection.GetOpposite().ToIntVec();
                return dungeon.CorridorTiles.Contains(tile) &&
                       (gen.IgnoreReserved || !reservedTiles.Contains(tile)) &&
                       _anchorable.TileFree((_gridUid, _grid), tile, DungeonSystem.CollisionLayer, DungeonSystem.CollisionMask) &&
                       dungeon.CorridorTiles.Contains(tile + inward) &&
                       dungeon.CorridorTiles.Contains(tile + inward * 2) &&
                       HasWallOrWindow(tile + wallDirection.ToIntVec()) &&
                       !HasAirlockNearby(tile);
            }

            var corridorTiles = dungeon.CorridorTiles.OrderBy(tile => tile.Y).ThenBy(tile => tile.X);
            foreach (var start in corridorTiles)
            {
                foreach (var wallDirection in wallDirections)
                {
                    if (!IsValidCorridorTile(start, wallDirection))
                        continue;

                    var wallVector = wallDirection.ToIntVec();
                    var lineDirection = new Vector2i(-wallVector.Y, wallVector.X);

                    // Draw lines along corridors
                    if (IsValidCorridorTile(start - lineDirection, wallDirection))
                        continue;

                    var run = new List<Vector2i>();
                    for (var tile = start; IsValidCorridorTile(tile, wallDirection); tile += lineDirection)
                    {
                        run.Add(tile);
                    }

                    // Split lines into sized sections
                    for (var offset = 0; offset + gen.Length <= run.Count; offset += gen.Length)
                    {
                        if (!random.Prob(gen.Prob))
                            continue;

                        var spawns = new List<(Vector2i Tile, EntProtoId[] Entities)>(gen.Length);

                        // Pick and spawn entities
                        for (var i = 0; i < gen.Length; i++)
                        {
                            var tile = run[offset + i];
                            spawns.Add((tile, _entTable.GetSpawns(contents, random).ToArray()));
                        }

                        var facing = wallDirection.GetOpposite().ToAngle();
                        foreach (var (tile, entities) in spawns)
                        {
                            var coordinates = _maps.GridTileToLocal(_gridUid, _grid, tile);
                            foreach (var entity in entities)
                            {
                                _entManager.SpawnAttachedTo(entity, coordinates, rotation: facing);
                            }
                        }

                        await SuspendDungeon();
                        if (!ValidateResume())
                            return;
                    }
                }
            }
        }
    }
}
