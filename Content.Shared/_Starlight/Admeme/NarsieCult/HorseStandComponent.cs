using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Admeme.NarsieCult;

/// <summary>
/// Lets a creature switch between a "Normal" stage and a "Standing up" stage with an action.
/// Entering the standing stage plays the changeling transformation sound, shows a popup over the
/// creature, swaps its sprite (see <see cref="HorseStandVisuals"/>), makes it much faster and grants
/// the eye-removal, stun, restrain and sleep actions. Leaving the stage undoes all of that.
/// Hands, damage, bleeding and devouring are handled by other components and are unaffected by the stage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(HorseStandSystem))]
public sealed partial class HorseStandComponent : Component
{
    /// <summary>
    /// The action that toggles the stage. Granted on MapInit.
    /// </summary>
    [DataField]
    public EntProtoId ToggleAction = "ActionHorseStand";

    /// <summary>
    /// Spawned entity for <see cref="ToggleAction"/>. Networked so the client can update the toggle icon.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    /// <summary>
    /// True while the creature is in the "Standing up" stage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Standing;

    /// <summary>
    /// Popup shown over the creature when it starts to stand up.
    /// </summary>
    [DataField]
    public LocId StandPopup = "horse-stand-popup";

    /// <summary>
    /// Played when the creature starts to stand up (the changeling transformation sound).
    /// </summary>
    [DataField]
    public SoundSpecifier StandSound = new SoundPathSpecifier("/Audio/Effects/Changeling/changeling_transform.ogg");

    /// <summary>
    /// Multiplier applied to walk and sprint speed while standing.
    /// </summary>
    [DataField]
    public float StandingSpeedModifier = 2f;

    /// <summary>
    /// Actions granted only while standing: eye removal, stun, restrain and sleep.
    /// </summary>
    [DataField]
    public List<EntProtoId> StandingActions = new()
    {
        "ActionHorseEyes",
        "ActionHorseStun",
        "ActionHorseRestrain",
        "ActionHorseSleep",
    };

    /// <summary>
    /// The spawned entities for <see cref="StandingActions"/>. Cleared when the creature goes back to normal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> StandingActionEntities = new();

    /// <summary>
    /// Played at the victim when their eyes are removed.
    /// </summary>
    [DataField]
    public SoundSpecifier EyesSound = new SoundPathSpecifier("/Audio/Effects/gib3.ogg");

    /// <summary>
    /// How long the stun power stuns and knocks down its target.
    /// </summary>
    [DataField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Played when the stun power is used successfully.
    /// </summary>
    [DataField]
    public SoundSpecifier StunSound = new SoundPathSpecifier("/Audio/Weapons/punch3.ogg");

    /// <summary>
    /// Played when the sleep power is used successfully. Must be mono for positional playback.
    /// </summary>
    [DataField]
    public SoundSpecifier SleepSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");

    /// <summary>
    /// The cuffs placed on a target by the restrain power. Applied instantly, with no do-after,
    /// but they are real cuffs, so the normal uncuff and break-out rules apply.
    /// </summary>
    [DataField]
    public EntProtoId RestraintPrototype = "HorseBindingCuffs";
}
