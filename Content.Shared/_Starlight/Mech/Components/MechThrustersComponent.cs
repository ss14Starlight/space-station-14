using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Mech.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class MechThrustersComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public bool ThrustersEnabled = false;

    /// <summary>
    /// Charge draw per second
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("drawRate")]
    public float DrawRate = 40f;

    [DataField]
    public EntProtoId MechToggleThrustersAction = "ActionMechToggleThrusters";

    [DataField] public EntityUid? MechToggleThrustersActionEntity;
}
