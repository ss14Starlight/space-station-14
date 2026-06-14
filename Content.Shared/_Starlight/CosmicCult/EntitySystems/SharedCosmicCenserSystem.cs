using System.Linq;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Shared._Starlight.CosmicCult.EntitySystems;

public sealed class SharedCosmicCenserSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicCenserComponent, SolutionTransferAttemptEvent>(OnSolutionTransferAttempt);
    }

    private void OnSolutionTransferAttempt(Entity<CosmicCenserComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner)
            return;

        var solution = args.SolutionEntity.Comp.Solution;

        if (solution.Contents.Any(sol => sol.Reagent.Prototype != ent.Comp.RefillReagent))
            args.Cancel("This solution contains unsuitable reagents!");
    }
}
