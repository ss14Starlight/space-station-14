using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FelionoidPounceComponent : Component
{
    /// <summary>
    /// The action prototype granted for starting a pounce.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId Action = "FelionoidPounce";

    [ViewVariables, AutoNetworkedField] public EntityUid? ActionEntity;

    /// <summary>
    /// The movement speed used while pouncing, in tiles per second.
    /// </summary>
    [DataField] public float JumpSpeed = 15f;

    /// <summary>
    /// How long a pounced target remains knocked down, in seconds.
    /// </summary>
    [DataField] public float KnockdownDuration = 2f;

    [ViewVariables] public EntityUid? ChargingTarget;

    [ViewVariables] public EntityUid? PouncingTarget;

    [ViewVariables] public TimeSpan PounceEndTime;
}
