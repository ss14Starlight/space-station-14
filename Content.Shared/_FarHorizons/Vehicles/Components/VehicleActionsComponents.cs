using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VehicleActionsComponent : Component
{
    
    [DataField, AutoNetworkedField]
    public EntProtoId HornVehicleAction = "ActionVehicleHorn";
    
    [DataField, AutoNetworkedField] public EntityUid? HornVehicleActionEntity;

    [DataField, AutoNetworkedField]
    public EntProtoId TurnKeysAction = "ActionTurnKeys";
    
    [DataField, AutoNetworkedField] public EntityUid? TurnKeysActionEntity;

    [DataField, AutoNetworkedField]
    public EntProtoId ToggleTrunkAction = "ActionToggleTrunk";
    
    [DataField, AutoNetworkedField] public EntityUid? ToggleTrunkActionEntity;
}