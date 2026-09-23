using Content.Shared._Funkystation.Stains.Components;
using Content.Shared._Funkystation.Stains.Systems;
using Content.Shared.Chemistry.Components;

namespace Content.Server._Funkystation.Stains;

public sealed partial class StainSystem : SharedStainSystem
{
    protected override void OnStainSolutionChanged(Entity<StainableComponent> ent, Entity<SolutionComponent> solution)
    {
        base.OnStainSolutionChanged(ent, solution);

        _flammableStains.OnStainSolutionChanged(ent.Owner, solution.Comp.Solution);
    }
}
