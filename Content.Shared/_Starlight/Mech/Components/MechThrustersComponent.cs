using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Mech.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechThrustersComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public bool ThrustersEnabled = false;

    /// <summary>
    /// Charge draw per second
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("drawRate")]
    public float DrawRate = 2f;

    [DataField]
    public EntProtoId MechToggleThrustersAction = "ActionMechToggleThrusters";

    [DataField, AutoNetworkedField]
    public EntityUid? MechToggleThrustersActionEntity;
}
