using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;
using Content.Shared.Access;

namespace Content.Shared.Doors.Electronics;

/// <summary>
/// Allows an entity's AccessReader to be configured via UI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // Starlight edit
public sealed partial class DoorElectronicsComponent : Component
{
    // Starlight Start
    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessGroupPrototype>> AccessGroups = new();
    // Starlight End
}

[Serializable, NetSerializable]
public sealed class DoorElectronicsUpdateConfigurationMessage : BoundUserInterfaceMessage
{
    public List<ProtoId<AccessLevelPrototype>> AccessList;

    public DoorElectronicsUpdateConfigurationMessage(List<ProtoId<AccessLevelPrototype>> accessList)
    {
        AccessList = accessList;
    }
}

[Serializable, NetSerializable]
public sealed class DoorElectronicsConfigurationState : BoundUserInterfaceState
{
    public List<ProtoId<AccessLevelPrototype>> AccessList;
    // Starlight Start
    public List<ProtoId<AccessGroupPrototype>> AccessGroups;
    public List<ProtoId<AccessLevelPrototype>> PressedAccessList;
    // Starlight End
    public DoorElectronicsConfigurationState(List<ProtoId<AccessLevelPrototype>> accessList, List<ProtoId<AccessGroupPrototype>> accessGroups, List<ProtoId<AccessLevelPrototype>> pressedAccessList) // Starlight edit
    {
        AccessList = accessList;
        // Starlight Start
        AccessGroups = accessGroups;
        PressedAccessList = pressedAccessList;
        // Starlight End
    }
}

[Serializable, NetSerializable]
public enum DoorElectronicsConfigurationUiKey : byte
{
    Key
}
