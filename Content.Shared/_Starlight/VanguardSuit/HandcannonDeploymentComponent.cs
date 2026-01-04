using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.VanguardSuit;

/// <summary>
/// Component for clothing that can deploy a handcannon after a DoAfter.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HandcannonDeploymentComponent : Component
{
    /// <summary>
    /// The handcannon prototype to spawn.
    /// </summary>
    [DataField]
    public EntProtoId HandcannonPrototype = "WeaponPistolCHIMP";

    /// <summary>
    /// How long the deployment takes.
    /// </summary>
    [DataField]
    public TimeSpan DeployDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The action entity for deploying.
    /// </summary>
    [DataField]
    public EntProtoId? DeployAction = "ActionDeployVanguardHandcannon";

    /// <summary>
    /// The spawned action entity.
    /// </summary>
    [DataField]
    public EntityUid? DeployActionEntity;

    /// <summary>
    /// The entity wearing this component.
    /// </summary>
    [DataField]
    public EntityUid? Wearer;
}
