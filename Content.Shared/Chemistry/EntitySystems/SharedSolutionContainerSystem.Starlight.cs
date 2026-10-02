using Content.Shared.Chemistry.Components;

namespace Content.Shared.Chemistry.EntitySystems;

/// <summary>
/// Part of Chemistry system deal with SolutionContainers
/// </summary>
public abstract partial class SharedSolutionContainerSystem : EntitySystem
{
    // Funky start
    public void BurnFlammableReagents(Entity<SolutionComponent> soln, float fraction)
    {
        soln.Comp.Solution.BurnFlammableReagents(fraction, PrototypeManager);
        UpdateChemicals(soln);
    }
    // Funky end
}
