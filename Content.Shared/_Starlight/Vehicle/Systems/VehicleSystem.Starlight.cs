using System.Linq;
using Content.Shared._Starlight.Vehicle;
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

    public bool HasVehicleKey(Entity<GenericKeyedVehicleComponent?> vehicle) =>
        Resolve(vehicle, ref vehicle.Comp, false) &&
        _container.TryGetContainer(vehicle, vehicle.Comp.ContainerId, out var slot) &&
        slot.ContainedEntities.Any(key => _entityWhitelist.IsWhitelistPass(vehicle.Comp.KeyWhitelist, key));

    public string? GetVehicleKeySlot(EntityUid vehicle) =>
        TryComp<GenericKeyedVehicleComponent>(vehicle, out var keyed) ? keyed.ContainerId : null;

    public bool SetIgnitionHandBlockers(EntityUid vehicle, bool enabled)
    {
        if (_timing.ApplyingState || GetOperatorOrNull(vehicle) is not { } driver)
            return !enabled;

        if (!enabled)
        {
            UnblockHands(vehicle, driver);
            return true;
        }

        return _vehicleQuery.TryComp(vehicle, out var comp) && TryBlockHands((vehicle, comp), driver);
    }

    public EntityUid? GetOccupiedVehicle(EntityUid? occupant)
    {
        if (occupant is not { } uid)
            return null;

        if (_operatorQuery.TryComp(uid, out var op) && op.Vehicle is { } driven)
            return driven;

        if (_container.TryGetContainingContainer(uid, out var container) &&
            HasComp<VehiclePassengerSeatsComponent>(container.Owner) &&
            TryGetOperatorContainer(container.Owner, out var seats) && seats == container)
        {
            return container.Owner;
        }

        return null;
    }

    public EntityUid? GetRideVehicle(EntityUid? occupant) =>
        GetOccupiedVehicle(occupant) is { } vehicle && HasComp<VehicleRideComponent>(vehicle) ? vehicle : null;

    public EntityUid GetExposedOperatorOrSelf(EntityUid target) => HasComp<VehicleRideComponent>(target) && HasComp<StrapVehicleComponent>(target)
            ? GetOperatorOrNull(target) ?? target
            : target;
}
