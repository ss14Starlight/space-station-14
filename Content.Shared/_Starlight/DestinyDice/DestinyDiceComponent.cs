using Content.Shared.Popups;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.DestinyDice;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DestinyDiceComponent : Component
{
    /// <summary>
    /// List of effect groups that may trigger when the die is rolled.
    /// </summary>
    [DataField("groups")]
    public List<DestinyDiceEffectGroup> EffectGroups = []; // Not networked due to EntityEffect not being serializable.

    /// <summary>
    /// Effect group preset prototype to populate <see cref="EffectGroups"/> with, if defined to make prototyping simpler.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<DestinyDicePresetPrototype>? Preset;

    /// <summary>
    /// Tracker to prevent <see cref="Robust.Shared.GameObjects.ComponentStartup"/> from adding preset groups more than once.
    /// </summary>
    [ViewVariables] public bool PresetAdded;

    /// <summary>
    /// Message that pops up when the rolled value has no associated effect/groups.
    /// </summary>
    [DataField, AutoNetworkedField] public string? NoEffectMessage;

    /// <summary>
    /// Popup type for no effect message.
    /// </summary>
    [DataField, AutoNetworkedField] public PopupType NoEffectPopupType = PopupType.Small;

    /// <summary>
    /// Message that pops up when trying to roll the die while on cooldown.
    /// </summary>
    [DataField, AutoNetworkedField] public string? CooldownMessage;

    /// <summary>
    /// Popup type for cooldown message.
    /// </summary>
    [DataField, AutoNetworkedField] public PopupType CooldownPopupType = PopupType.Small;

    /// <summary>
    /// Message that pops up when trying to roll the die while effects are being triggered.
    /// </summary>
    [DataField, AutoNetworkedField] public string? BusyMessage;

    /// <summary>
    /// Popup type for busy message.
    /// </summary>
    [DataField, AutoNetworkedField] public PopupType BusyPopupType = PopupType.Small;

    /// <summary>
    /// If defined, the amount of seconds before the die can trigger effect groups on roll again.
    /// </summary>
    [DataField, AutoNetworkedField] public float? RollDelay;

    /// <summary>
    /// If defined, the amount of seconds to wait before attempting to roll a group with the current value.
    /// </summary>
    [DataField, AutoNetworkedField] public float? GroupDelay;

    /// <summary>
    /// The amount of seconds to wait after the die lands before starting the effect group, on top of <see cref="GroupDelay"/>.
    /// </summary>
    [DataField, AutoNetworkedField] public float LandDelay = 1f;

    /// <summary>
    /// The last person to roll the die and trigger an effect group.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? ActiveRoller;

    /// <summary>
    /// The last grid the die landed on when an effect group was triggered.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? ActiveGrid;

    /// <summary>
    /// The last map the die landed on when an effect group was triggered;
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? ActiveMap;

    /// <summary>
    /// The last grid the die roller was on when the die landed and an effect group was triggered.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? RollerGrid;

    /// <summary>
    /// The time at which effects are allowed to be triggered again. Determined via <see cref="RollDelay"/>.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public TimeSpan NextAllowedRollTime;

    /// <summary>
    /// The time at which the next queued effect will trigger.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public TimeSpan NextEffectTriggerTime;

    /// <summary>
    /// The time at which the queued effect group will execute.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public TimeSpan GroupStartTime;

    /// This is <see langword="true"/> if waiting for an event to fire before continuing effect execution.
    /// <remarks>
    /// This property is not read-only to make so you can free the die from waiting indefinitely
    /// should something go wrong or something.
    /// </remarks>
    [ViewVariables, AutoNetworkedField] public bool WaitingForEffectEnd;

    /// <summary>
    /// Tracks if the die is actively triggering effects.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool IsActive;

    /// <summary>
    /// Tracks if the die is waiting to execute a group.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool IsPending;

    /// <summary>
    /// Mirror of <see cref="Content.Shared.Dice.DiceComponent.CurrentValue"/> for faster lookup.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public int CurrentValue;

    /// <summary>
    /// A record of all effects (by index in <see cref="CurrentEffectGroup"/>) that have been triggered with this roll and whether they successfully triggered or not.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public Dictionary<int, bool> EffectResults = [];

    /// <summary>
    /// Index into <see cref="EffectGroups"/> of the current active effect group, or -1 if none.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public int CurrentGroupIndex = -1;

    /// <summary>
    /// Index into the current group's effects of the current active effect, or -1 if none.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public int CurrentEffectIndex = -1;

    /// <summary>
    /// Index into the current group's effects of the next effect that needs to be executed.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public int NextEffectIndex;

    /*
     * Roll/trigger counters live here instead of on the group/effect objects so they get rolled back with the rest
     * of the state during prediction. Missing entries fall back to the TimesRolled/TimesTriggered starting values
     * defined on the group/effect.
     */
    /// <summary>
    /// Number of times each effect group (by index in <see cref="EffectGroups"/>) has been rolled and picked.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public Dictionary<int, int> GroupTimesRolled = [];

    /// <summary>
    /// Number of times each effect group (by index in <see cref="EffectGroups"/>) has been triggered.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public Dictionary<int, int> GroupTimesTriggered = [];

    /// <summary>
    /// Number of times each effect has been rolled and picked.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public Dictionary<DestinyDiceEffectKey, int> EffectTimesRolled = [];

    /// <summary>
    /// Number of times each effect has been triggered.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public Dictionary<DestinyDiceEffectKey, int> EffectTimesTriggered = [];

    /// <summary>
    /// Tracks the current active effect group.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public DestinyDiceEffectGroup? CurrentEffectGroup =>
        CurrentGroupIndex >= 0 && CurrentGroupIndex < EffectGroups.Count ? EffectGroups[CurrentGroupIndex] : null;

    /// <summary>
    /// Tracks the current active effect.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public DestinyDiceEffect? CurrentEffect =>
        CurrentEffectGroup is { } group && CurrentEffectIndex >= 0 && CurrentEffectIndex < group.Effects.Count
            ? group.Effects[CurrentEffectIndex]
            : null;
}

/// <summary>
/// Identifies an effect by its group's index in <see cref="DestinyDiceComponent.EffectGroups"/> and its index in that group.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct DestinyDiceEffectKey(int Group, int Effect);
