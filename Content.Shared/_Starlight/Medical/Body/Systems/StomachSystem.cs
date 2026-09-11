using Content.Shared._Starlight.Medical.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Whitelist;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared._Starlight.Medical.Body.Events;

namespace Content.Shared._Starlight.Medical.Body.Systems;

public sealed class StomachSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

    public const string DefaultSolutionName = "stomach";

    public bool CanTransferSolution(Entity<StomachComponent?, SolutionManagerComponent?> entity, Solution solution)
    {
        return Resolve(entity, ref entity.Comp1, logMissing: false)
            && _solutionContainerSystem.ResolveSolution((entity, entity.Comp2), DefaultSolutionName, ref entity.Comp1.Solution, out var stomachSolution)
            // TODO: For now no partial transfers. Potentially change by design
            && stomachSolution.CanAddSolution(solution);
    }

    public bool TryTransferSolution(Entity<StomachComponent?, SolutionManagerComponent?> entity, Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false)
            || !_solutionContainerSystem.ResolveSolution((entity, entity.Comp2), DefaultSolutionName, ref entity.Comp1.Solution)
            || !CanTransferSolution(entity, solution))
            return false;

        return _solutionContainerSystem.TryAddSolution(entity.Comp1.Solution.Value, solution);
    }
}
