using System.Linq;
using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;

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

        if (Deleted(args.SolutionEntity.Owner) ||
            !TryComp<SolutionComponent>(args.SolutionEntity.Owner, out var solutionComp))
        {
            args.Cancel(Loc.GetString(ent.Comp.Popup));
            return;
        }

        var solution = solutionComp.Solution;

        if (solution == null || solution.Contents == null)
        {
            args.Cancel(Loc.GetString(ent.Comp.Popup));
            return;
        }

        if (solution.Contents.Any(sol => !ent.Comp.Reagents.Contains(sol.Reagent.Prototype)))
            args.Cancel(Loc.GetString(ent.Comp.Popup));
    }
}
