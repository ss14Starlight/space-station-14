using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Mech.Components;

/// <summary>
/// Component for mech equipment that provides a passive effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechPassiveEquipmentComponent : Component
{
    [DataField("addComponents", required: true)]
    public ComponentRegistry AddComponents = new();

    [DataField]
    public bool ComponentsProvided = false;
}