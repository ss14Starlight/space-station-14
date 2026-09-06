using Content.Shared.Popups;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.DestinyDice;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DestinyDiceComponent : Component
{
    /*
     * TODO: Make an engine PR to finally permit vvwriting !type:ClassName and have it actually work.
     * That is why effect prototypes are handled like this, to make it easier to vvwrite once this is supported.
     */
    /// List of effect groups that may trigger when the die is rolled.
    [DataField("groups")] public List<DestinyDiceEffectGroup> EffectGroups = []; // Not networked due to EntityEffect not being serializable.
    /// Effect group preset prototype to populate <see cref="EffectGroups"/> with, if defined to make prototyping simpler.
    [DataField, AutoNetworkedField] public ProtoId<DestinyDicePresetPrototype>? Preset;
    /// Tracker to prevent <see cref="Robust.Shared.GameObjects.ComponentStartup"/> from adding preset groups more than once.
    [ViewVariables, AutoNetworkedField] public bool PresetAdded;

    /// Message that pops up when the rolled value has no associated effect/groups.
    [DataField, AutoNetworkedField] public string? NoEffectMessage;
    /// Popup type for no effect message.
    [DataField, AutoNetworkedField] public PopupType NoEffectPopupType = PopupType.Small;
    /// Message that pops up when trying to roll the die while on cooldown.
    [DataField, AutoNetworkedField] public string? CooldownMessage;
    /// Popup type for cooldown message.
    [DataField, AutoNetworkedField] public PopupType CooldownPopupType = PopupType.Small;
    /// Message that pops up when trying to roll the die while effects are being triggered.
    [DataField, AutoNetworkedField] public string? BusyMessage;
    /// Popup type for busy message.
    [DataField, AutoNetworkedField] public PopupType BusyPopupType = PopupType.Small;
    /// If defined, the amount of seconds before the die can trigger effect groups on roll again.
    [DataField, AutoNetworkedField] public float? RollDelay;
    /// If defined, the amount of seconds to wait before attempting to roll a group with the current value.
    [DataField, AutoNetworkedField] public float? GroupDelay;

    /// The last person to roll the die and trigger an effect group.
    [ViewVariables(VVAccess.ReadOnly)] public EntityUid? ActiveRoller;
    /// The last grid the die landed on when an effect group was triggered.
    [ViewVariables(VVAccess.ReadOnly)] public EntityUid? ActiveGrid;
    /// The last map the die landed on when an effect group was triggered;
    [ViewVariables(VVAccess.ReadOnly)] public EntityUid? ActiveMap;
    /// The last grid the die roller was on when the die landed and an effect group was triggered.
    [ViewVariables(VVAccess.ReadOnly)] public EntityUid? RollerGrid;

    /// The time at which effects are allowed to be triggered again. Determined via <see cref="RollDelay"/>.
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextAllowedRollTime;
    /// The time at which the next queued effect will trigger.
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextEffectTriggerTime;
    /// The time at which the queued effect group will execute.
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan GroupStartTime;

    /// This is <see langword="true"/> if waiting for an event to fire before continuing effect execution.
    /// <remarks>
    /// This property is not read-only to make so you can free the die from waiting indefinitely
    /// should something go wrong or something.
    /// </remarks>
    [ViewVariables] public bool WaitingForEffectEnd;

    /// Tracks if the die is actively triggering effects.
    [ViewVariables(VVAccess.ReadOnly)] public bool IsActive;
    /// Tracks if the die is waiting to execute a group.
    [ViewVariables(VVAccess.ReadOnly)] public bool IsPending;

    /// Mirror of <see cref="Content.Shared.Dice.DiceComponent.CurrentValue"/> for faster lookup.
    [ViewVariables(VVAccess.ReadOnly)] public int CurrentValue;

    /// A record of all effects that have been triggered with this roll and whether they successfully triggered or not.
    [ViewVariables(VVAccess.ReadOnly)] public Dictionary<DestinyDiceEffect, bool> EffectResults = [];
    /// Tracks the current active effect group.
    [ViewVariables(VVAccess.ReadOnly)] public DestinyDiceEffectGroup? CurrentEffectGroup;
    /// Tracks the current active effect.
    [ViewVariables(VVAccess.ReadOnly)] public DestinyDiceEffect? CurrentEffect;
    /// Tracks the remaining effects that need to be executed
    [ViewVariables(VVAccess.ReadOnly)] public Queue<DestinyDiceEffect> EffectQueue = [];
}
