using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Vehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleRadarComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionVehicleRadar";

    public EntityUid? ActionEntity;
}

public sealed partial class ToggleVehicleRadarEvent : InstantActionEvent;
