using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FelionoidPounceComponent : Component
{
    [DataField, AutoNetworkedField] public EntProtoId Action = "FelionoidPounce";

    [ViewVariables, AutoNetworkedField] public EntityUid? ActionEntity;

    [DataField] public float JumpSpeed = 15f;

    [DataField] public float KnockdownDuration = 2f;

    [ViewVariables] public EntityUid? ChargingTarget;

    [ViewVariables] public EntityUid? PouncingTarget;

    [ViewVariables] public TimeSpan PounceEndTime;
}
