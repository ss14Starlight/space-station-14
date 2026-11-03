using Content.Client._Starlight.Plumbing.Components;
using Content.Client._Starlight.Plumbing.UI;
using Content.Client._Starlight.Verbs.UI;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;

namespace Content.Client._Starlight.Plumbing.EntitySystems;

public sealed partial class PlumbingSmartDispenserSystem
{
    [SubscribeLocalEvent]
    private void OnGetVerbMenuEntries(Entity<PlumbingSmartDispenserComponent> ent, ref GetVerbMenuEntriesEvent args)
    {
        if (!CanContextDispense(args.User, ent)
            || !_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution))
            return;

        // Reagents with different data (e.g. blood DNA) share one entry.
        var reagentIds = new HashSet<string>();
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (quantity > FixedPoint2.Zero)
                reagentIds.Add(reagent.Prototype);
        }

        var entries = new List<(string Label, ReagentPrototype Proto)>();
        foreach (var id in reagentIds)
        {
            if (!_prototypeManager.TryIndex<ReagentPrototype>(id, out var proto))
                continue;

            if (!ent.Comp.ReagentLabels.TryGetValue(id, out var label) || string.IsNullOrWhiteSpace(label))
                label = proto.LocalizedName;

            entries.Add((label, proto));
        }

        // Same order as the dispenser window.
        entries.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

        foreach (var (label, proto) in entries)
        {
            var reagentId = proto.ID;
            args.Entries.Add(new PlumbingSmartDispenserReagentMenuElement(
                label,
                proto.SubstanceColor,
                () => GetFillFraction(ent, proto),
                () => GetReagentTooltip(ent, proto))
            {
                OnUse = () => RequestContextDispense(ent.Owner, reagentId),
                CloseMenu = false,
            });
        }
    }

    private FixedPoint2 GetStored(Entity<PlumbingSmartDispenserComponent> ent, ReagentPrototype proto) =>
        _solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution)
            ? solution.GetTotalPrototypeQuantity(proto.ID)
            : FixedPoint2.Zero;

    private float GetFillFraction(Entity<PlumbingSmartDispenserComponent> ent, ReagentPrototype proto) =>
        ent.Comp.MaxPerReagent > FixedPoint2.Zero
            ? GetStored(ent, proto).Float() / ent.Comp.MaxPerReagent.Float()
            : 1f;

    private string GetReagentTooltip(Entity<PlumbingSmartDispenserComponent> ent, ReagentPrototype proto) =>
        Loc.GetString("plumbing-smart-dispenser-row-tooltip",
            ("reagent", proto.LocalizedName),
            ("stored", GetStored(ent, proto)),
            ("max", ent.Comp.MaxPerReagent));

    private void RequestContextDispense(EntityUid dispenser, string reagentId)
    {
        var amount = EnsureComp<PlumbingSmartDispenserLocalStateComponent>(dispenser).SelectedDispenseAmount;
        RaisePredictiveEvent(new PlumbingSmartDispenserContextDispenseEvent(GetNetEntity(dispenser), reagentId, amount));
    }
}
