using Content.Shared._Funkystation.WallStains;
using Content.Shared._Funkystation.WallStains.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Map.Components;

namespace Content.Server._Funkystation.WallStains.Systems;

public sealed partial class WallStainSystem
{
    #region Starlight
    private static readonly Vector2i[] _wallCleaningOffsets =
    [
        Vector2i.Up,
        Vector2i.Down,
        Vector2i.Left,
        Vector2i.Right,
        Vector2i.UpLeft,
        Vector2i.UpRight,
        Vector2i.DownLeft,
        Vector2i.DownRight,
    ];

    [Dependency] private EntityQuery<StainedWallComponent> _stainedWallQuery = default!;

    private readonly HashSet<EntityUid> _evaporatingStains = [];
    private readonly List<EntityUid> _evaporatingStainsSnapshot = [];
    private readonly List<EntityUid> _wallsToClean = [];

    private void CleanWallStains(EntityUid target, bool cleanAdjacent)
    {
        _wallsToClean.Clear();
        _wallsToClean.Add(target);

        if (cleanAdjacent)
        {
            var xform = Transform(target);
            if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
            {
                var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
                foreach (var offset in _wallCleaningOffsets)
                {
                    var anchored = _map.GetAnchoredEntities(gridUid, grid, tile + offset);
                    while (anchored.MoveNext(out var entity))
                    {
                        if (_stainedWallQuery.HasComponent(entity.Value))
                            _wallsToClean.Add(entity.Value);
                    }
                }
            }
        }

        // Collect before cleaning because the event removes StainedWallComponent from each wall.
        foreach (var wall in _wallsToClean)
            RaiseLocalEvent(wall, new CleanWallStainsEvent(transformToWater: false));

        _wallsToClean.Clear();
    }

    private void OnStainMapInit(Entity<WallStainComponent> entity, ref MapInitEvent args)
    {
        if (!_solution.TryGetSolution(entity.Owner, entity.Comp.SolutionName, out _, out var solution))
            return;

        UpdateEvaporationTracking(entity.Owner, solution);
        if (solution.Volume > FixedPoint2.Zero)
            UpdateVisuals(entity.Owner, entity.Comp, solution);
    }

    private void OnStainShutdown(Entity<WallStainComponent> entity, ref ComponentShutdown args)
        => _evaporatingStains.Remove(entity.Owner);

    private void OnStainSolutionChanged(Entity<WallStainComponent> entity, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id != entity.Comp.SolutionName)
            return;

        var solution = args.Solution.Comp.Solution;
        UpdateEvaporationTracking(entity.Owner, solution);
        if (solution.Volume > FixedPoint2.Zero)
            UpdateVisuals(entity.Owner, entity.Comp, solution);
    }

    private void UpdateEvaporationTracking(EntityUid uid, Solution solution)
    {
        if (solution.Volume <= FixedPoint2.Zero ||
            solution.GetTotalPrototypeQuantity(_waterReagent) > FixedPoint2.Zero ||
            solution.GetTotalPrototypeQuantity(_spaceCleanerReagent) > FixedPoint2.Zero)
        {
            _evaporatingStains.Add(uid);
            return;
        }

        _evaporatingStains.Remove(uid);
    }
    #endregion
}
