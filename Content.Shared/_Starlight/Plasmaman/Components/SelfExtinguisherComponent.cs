using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Plasmaman.Components;

/// <summary>
/// Adds a wearable action that extinguishes the wearer while the suit still has charges.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SelfExtinguisherComponent : Component
{
    /// <summary>
    /// Prototype ID of the action entity granted to the wearer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionSelfExtinguish";

    /// <summary>
    /// Instantiated action entity tracked for removal when the component is removed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Time between uses of the self-extinguish action.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Cooldown;

    /// <summary>
    /// Timestamp after which the next extinguish use is allowed.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextExtinguish = TimeSpan.Zero;

    /// <summary>
    /// Minimum time between "not ready yet" popup messages shown to the player.
    /// </summary>
    [DataField]
    public TimeSpan PopupCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Timestamp after which the next popup message may be shown.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextPopup = null;

    /// <summary>
    /// Sound played when the self-extinguish action is used.
    /// </summary>
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
