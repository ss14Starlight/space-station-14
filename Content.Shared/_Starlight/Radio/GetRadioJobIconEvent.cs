using Content.Shared.Inventory;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Radio;

/// <summary>
/// Raised on the radio message source
/// to let things override the job icon shown on the radio.
/// </summary>
public sealed class GetRadioJobIconEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;

    public ProtoId<JobIconPrototype>? JobIcon;
}
