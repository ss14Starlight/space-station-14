using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared._Starlight.Plumbing.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Plumbing.EntitySystems;

/// <summary>
///     Handles the plumbing smart dispenser: pulls all reagents from the network,
///     stores up to a per-reagent cap, supports held container dispensing and label matching dispensing
/// </summary>
[UsedImplicitly]
public sealed partial class PlumbingSmartDispenserSystem : SharedPlumbingSmartDispenserSystem
{
    /// <summary>
    /// Caps or denies pulls for reagents that are at or near the per-reagent limit.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnPullInto(Entity<PlumbingSmartDispenserComponent> ent, ref PlumbingPullIntoAttemptEvent args)
    {
        if (!_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution))
            return;

        var current = solution.GetReagentQuantity(new ReagentId(args.ReagentPrototype, null));
        var room = ent.Comp.MaxPerReagent - current;

        if (room <= FixedPoint2.Zero)
        {
            args.Cancelled = true;
            return;
        }

        if (room < args.MaxAllowed)
            args.MaxAllowed = room;
    }
}
