using Content.Shared._Funkystation.Footprints;
using Content.Shared.Chemistry.Components;
using Content.Shared.Timing;
using Robust.Shared.Map.Components;

namespace Content.Shared.Fluids;

public abstract partial class SharedAbsorbentSystem
{
    private static readonly Vector2i[] _plusCleaningOffsets =
    [
        Vector2i.Up,
        Vector2i.Down,
        Vector2i.Left,
        Vector2i.Right,
    ];

    private static readonly Vector2i[] _squareCornerCleaningOffsets =
    [
        Vector2i.UpLeft,
        Vector2i.UpRight,
        Vector2i.DownLeft,
        Vector2i.DownRight,
    ];

    [Dependency] private EntityQuery<FootprintComponent> _footprintQuery = default!;

    private readonly List<EntityUid> _footprintsToClean = [];

    private void CleanAdjacentFootprints(
        Entity<AbsorbentComponent, UseDelayComponent?> absorbEnt,
        Entity<SolutionComponent> absorberSoln,
        EntityUid user,
        EntityUid target)
    {
        var pattern = absorbEnt.Comp1.FootprintCleaning;
        if (pattern == FootprintCleaningPattern.Target)
            return;

        var xform = Transform(target);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        _footprintsToClean.Clear();
        var tile = _mapSystem.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        CollectFootprints(gridUid, grid, tile, _plusCleaningOffsets);

        if (pattern == FootprintCleaningPattern.Square)
            CollectFootprints(gridUid, grid, tile, _squareCornerCleaningOffsets);

        // Collect before cleaning because each successful clean converts the footprint into an anchored puddle.
        // Reuse the normal transfer path without repeating its sound, lunge, popup, or use-delay work.
        foreach (var footprint in _footprintsToClean)
            TryPuddleInteract(absorbEnt, absorberSoln, user, footprint, primaryInteraction: false);

        _footprintsToClean.Clear();
    }

    private void CollectFootprints(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i origin,
        Vector2i[] offsets)
    {
        foreach (var offset in offsets)
        {
            var anchored = _mapSystem.GetAnchoredEntities(gridUid, grid, origin + offset);
            while (anchored.MoveNext(out var entity))
            {
                if (_footprintQuery.HasComponent(entity.Value))
                    _footprintsToClean.Add(entity.Value);
            }
        }
    }
}
