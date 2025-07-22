using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Hailer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HailerComponent : Component
{
    /// <summary>
    /// The message to broadcast
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string HailMessage = string.Empty;

    /// <summary>
    /// The sound
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SoundSpecifier? HailSound;

    /// <summary>
    /// Cooldown
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CooldownDuration = 30f;

    /// <summary>
    /// The action entity
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId<InstantActionComponent> Action = "ActionHailer";

    /// <summary>
    /// The spawned action entity
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}
