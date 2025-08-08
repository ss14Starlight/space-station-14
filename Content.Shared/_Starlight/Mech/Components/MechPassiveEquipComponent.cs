using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Mech.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class MechPassiveEquipComponent : Component
{
    [DataField("addOnToggle", required: true)]
    public ComponentRegistry AddOnToggle = new();
}