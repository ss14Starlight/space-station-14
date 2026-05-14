using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical;

/// <summary>
/// This is used for defibrillators intended to be equipped,
/// like gloves that can shock people
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WearableDefibrillatorComponent : Component
{
    /// <summary>
    /// How much damage is healed from getting zapped.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public DamageSpecifier ZapHeal = default!;

    /// <summary>
    /// The electrical damage from getting zapped.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ZapDamage = 5;

    /// <summary>
    /// How long the victim will be electrocuted after getting zapped.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan WritheDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// ID of the cooldown use delay.
    /// </summary>
    [DataField]
    public string DelayId = "defib-delay";

    /// <summary>
    /// Cooldown after using the defibrillator.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ZapDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the doafter for zapping someone takes.
    /// </summary>
    /// <remarks>
    /// This is synced with the audio; do not change one but not the other.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// If false cancels the doafter when moving.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowDoAfterMovement = true;

    /// <summary>
    /// Can the defibrilator be used on mobs in critical mobstate?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanDefibCrit = true;

    /// <summary>
    /// The sound to play when someone is zapped.
    /// </summary>
    [DataField]
    public SoundSpecifier? ZapSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");

    /// <summary>
    /// The sound to play when starting the doafter.
    /// </summary>
    [DataField]
    public SoundSpecifier? ChargeSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_charge.ogg");

    /// <summary>
    /// Defib failure sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? FailureSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_failed.ogg");

    /// <summary>
    /// Defib success sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? SuccessSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_success.ogg");

    [DataField]
    public SoundSpecifier? ReadySound = new SoundPathSpecifier("/Audio/Items/Defib/defib_ready.ogg");

    /// <summary>
    /// The action it will grant.
    /// </summary>
    [DataField]
    public EntProtoId Action = "ActionDefib";

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// What slot the defib will give an action when equipped in.
    /// </summary>
    [DataField]
    public SlotFlags RequiredSlot = SlotFlags.GLOVES;
}

/// <summary>
/// Event for the defib action.
/// </summary>
public sealed partial class DefibActionEvent : EntityTargetActionEvent { }
