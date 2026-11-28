using System.Linq;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Vehicle.Components;
using Content.Shared.Whitelist;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Vehicle.Systems;

public sealed partial class VehicleSystem
{
    private int GetBlockedHandCount(EntityUid vehicle, EntityUid pilot)
    {
        if (!_handBlockerQuery.TryComp(vehicle, out var blocker) ||
            _entityWhitelist.IsWhitelistPass(blocker.ExemptOperators, pilot))
            return 0;

        return Math.Max(0, blocker.BlockedHands);
    }

    private void ReleaseHandBlockers(EntityUid vehicle, EntityUid pilot)
    {
        foreach (var hand in _hands.EnumerateHands(pilot).ToArray())
        {
            if (!_hands.TryGetHeldItem(pilot, hand, out var held) ||
                !TryComp<VirtualItemComponent>(held, out var blocker) || blocker.BlockingEntity != vehicle)
                continue;

            // don't keep control after you're not on
            RemComp<UnremoveableComponent>(held.Value);
            _hands.DoDrop(pilot, hand, doDropInteraction: false, log: false);
        }
    }

    public void SetOperatorWhitelist(Entity<VehicleComponent?> ent, EntityWhitelist? whitelist)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;
        ent.Comp.OperatorWhitelist = whitelist;
        DirtyField(ent, ent.Comp, nameof(VehicleComponent.OperatorWhitelist));
        RefreshCanRun(ent);
    }
}
