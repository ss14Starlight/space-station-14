using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Mech.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MechSirenComponent : Component
{

    [DataField, AutoNetworkedField]
    public bool Toggled = false;

    [DataField]
    public EntProtoId MechToggleSirenAction = "ActionMechToggleSirens";

    [DataField, AutoNetworkedField]
    public EntityUid? MechToggleSirenActionEntity;
}
