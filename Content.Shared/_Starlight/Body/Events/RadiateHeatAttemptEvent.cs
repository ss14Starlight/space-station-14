using Content.Shared.Inventory;

namespace Content.Shared._Starlight.Body.Events;

// Event that allows us to block heat radiation
[ByRefEvent]
public record struct RadiateHeatAttemptEvent(EntityUid Uid) : IInventoryRelayEvent
{
    public readonly EntityUid Uid = Uid;
    public bool Cancelled = false;

    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;
}
