using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Admeme.Jumpscare;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedJumpscareSystem))]
public sealed partial class JumpscareComponent : Component
{
    /// <summary>
    /// List of available jumpscares.
    /// </summary>
    [DataField, AutoNetworkedField] public List<JumpscareData> Jumpscares = [];

    /// <summary>
    /// Can entity do a mass AOE jumpscare?
    /// </summary>
    [DataField, AutoNetworkedField] public bool CanMassJumpscare;

    /// <summary>
    /// How long until the mass jumpscare doafter finishes and triggers a mass jumpscare.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan MassJumpscareDoAfterTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Effective range of the mass AOE jumpscare.
    /// </summary>
    [DataField, AutoNetworkedField] public float MassRange;

    /// <summary>
    /// Can you jumpscare a dead target?
    /// </summary>
    [DataField, AutoNetworkedField] public bool WorksOnDead;

    /// <summary>
    /// Action used to jumpscare a target.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId Action = "ActionJumpscare";

    [ViewVariables, AutoNetworkedField] public EntityUid? ActionEntity;
}
