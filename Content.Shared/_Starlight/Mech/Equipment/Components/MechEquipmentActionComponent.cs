using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Mech.Equipment.Components;

/// <summary>
/// Used to add an action to a piece of mech equipment. The action will be granted to the pilot.
/// </summary>
/// <remarks>
/// Action handling should happen in equipment specific components.
/// This just manages granting/removing the action itself.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechEquipmentActionComponent : Component
{

    [DataField("actionId")]
    public EntProtoId EquipmentAction = "";

    [DataField, AutoNetworkedField] public EntityUid? EquipmentActionEntity;
}
