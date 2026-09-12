using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Atmos.Piping.Unary.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class GasCanisterHoseSlotComponent : Component
{
    /// <summary>The name of the container used to hold the hose.</summary>
    [DataField]
    public string ContainerName = "hose_slot";

    /// <summary>The slot containing the attached hose.</summary>
    [DataField]
    public ItemSlot HoseSlot = new();

    public bool AllowEject;
}
