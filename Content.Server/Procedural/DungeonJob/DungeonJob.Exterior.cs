using System.Threading.Tasks;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Content.Shared.Procedural;
using Content.Shared.Procedural.DungeonGenerators;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Procedural.DungeonJob;

public sealed partial class DungeonJob
{
    /// <summary>
    /// <see cref="ExteriorDunGen"/>
    /// </summary>
    private async Task<List<Dungeon>> GenerateExteriorDungen(Vector2i position, ExteriorDunGen dungen, HashSet<Vector2i> reservedTiles, Random random)
    {
        DebugTools.Assert(_grid.ChunkCount > 0);

        var aabb = new Box2i(_grid.LocalAABB.BottomLeft.Floored(), _grid.LocalAABB.TopRight.Floored());
        var angle = random.NextAngle();

        var distance = Math.Max(aabb.Width / 2f + 1f, aabb.Height / 2f + 1f);

        var startTile = new Vector2i(0, (int) distance).Rotate(angle);

        Vector2i? dungeonSpawn = null;

        // Gridcast
        SharedPathfindingSystem.GridCast(startTile, position, tile =>
        {
            if (!_maps.TryGetTileRef(_gridUid, _grid, tile, out var tileRef) ||
                _turf.IsSpace(tileRef.Tile))
            {
                return true;
            }

            dungeonSpawn = tile;
            return false;
        });

        if (dungeonSpawn == null)
        {
            return new List<Dungeon>()
            {
                Dungeon.Empty
            };
        }

        var config = _prototype.Index(dungen.Proto);
        var nextSeed = random.Next();

        // Starlight edit Start: Dont fail all generation if exterior fails
        try
        {
            var dungeons = await GetDungeons(dungeonSpawn.Value, config, config.Layers, reservedTiles, nextSeed, new Random(nextSeed));
            return dungeons;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _sawmill.Error(
                $"Exterior dungeon {dungen.Proto} failed to generate with seed {nextSeed} " +
                $"while generating parent dungeon {_gen} with seed {_seed} on {_entManager.ToPrettyString(_gridUid)}. " +
                $"Skipping exterior dungeon so parent generation can continue:\n{e}");

            return [Dungeon.Empty];
        }
        // Starlight edit End
    }
}
