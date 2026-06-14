using System.Linq;
using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Shared._Starlight.Chemistry.Systems;

public sealed class SharedRefillReagentFilterSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RefillReagentFilterComponent, SolutionTransferAttemptEvent>(OnSolutionTransferAttempt);
    }

    private void OnSolutionTransferAttempt(Entity<RefillReagentFilterComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner)
            return;

        var solution = args.SolutionEntity.Comp.Solution;

        if (solution.Contents.Any(sol => !ent.Comp.Reagents.Contains(sol.Reagent.Prototype)))
            args.Cancel(Loc.GetString(ent.Comp.Popup));
    }
}
