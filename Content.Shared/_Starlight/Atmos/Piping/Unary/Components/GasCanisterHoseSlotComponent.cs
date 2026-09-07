using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared.Atmos.Piping.Unary.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class GasCanisterHoseSlotComponent : Component
{
    [DataField]
    public string ContainerName = "hose_slot";

    [DataField]
    public ItemSlot HoseSlot = new();

    public bool AllowEject;
}
