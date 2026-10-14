using Content.Shared.Chemistry.Components;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class SolutionTransferSystem : EntitySystem
{
    // Multiple reagent whitelist
    private void OnRefillTransferAttempt(
        Entity<RefillableSolutionComponent> ent,
        ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner ||
            ent.Comp.ReagentWhitelist is not { } whitelist)
        {
            return;
        }

        foreach (var (reagent, _) in args.SolutionEntity.Comp.Solution)
        {
            if (whitelist.Contains(reagent.Prototype))
                continue;

            args.Cancel(Loc.GetString("comp-solution-transfer-reagent-not-allowed"));
            return;
        }
    }
}
