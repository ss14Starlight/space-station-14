using Content.Shared._Funkystation.WallStains.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.Server._Funkystation.WallStains.Systems;

public sealed partial class WallStainSystem : EntitySystem
{
    private void OnStainMapInit(Entity<WallStainComponent> entity, ref MapInitEvent args)
    {
        if (!_solution.TryGetSolution(entity.Owner, entity.Comp.SolutionName, out _, out var solution))
            return;

        UpdateEvaporationTracking(entity.Owner, solution);
        if (solution.Volume > FixedPoint2.Zero)
            UpdateVisuals(entity.Owner, entity.Comp, solution);
    }

    private void OnStainShutdown(Entity<WallStainComponent> entity, ref ComponentShutdown args) => _evaporatingStains.Remove(entity.Owner);

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
}
