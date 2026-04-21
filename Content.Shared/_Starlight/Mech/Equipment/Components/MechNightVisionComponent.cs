using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Mech.Equipment.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechNightVisionComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public bool EquipmentToggled = false;

    [DataField]
    [AutoNetworkedField]
    public bool EquipmentComponentAdded = false;

}
