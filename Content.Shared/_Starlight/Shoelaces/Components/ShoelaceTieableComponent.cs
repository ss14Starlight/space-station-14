using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Shoelaces.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ShoelaceTieableComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Tied = true;

    [DataField, AutoNetworkedField]
    public bool TiedTogether = false;

    [DataField]
    public float UntieSelfTime = 6f;

    [DataField]
    public float UntieAssistTime = 2f;

    [DataField]
    public float TieSelfTime = 4f;

    [DataField]
    public float TieTime = 2f;

    [DataField]
    public float TieTogetherTime = 6f;

    [DataField]
    public float KnockDownChance = 0.2f;

    /// <summary>
    /// Chance to untie shoes when knocked down.
    /// </summary>
    [DataField]
    public float ForceUntieChance = 0.1f;

    [DataField]
    public ProtoId<AlertPrototype> AlertTiedTogether = "ShoelacesTiedTogether";

    [DataField]
    public ProtoId<AlertPrototype> AlertUntied = "ShoelacesUntied";
}
