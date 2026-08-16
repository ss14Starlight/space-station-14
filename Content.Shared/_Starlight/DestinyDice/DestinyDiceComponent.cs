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
    [DataField("groups"), AutoNetworkedField] public List<DestinyDiceEffectGroup> EffectGroups = [];
    /// Effect group preset prototype to populate <see cref="EffectGroups"/> with, if defined to make prototyping simpler.
    [DataField, AutoNetworkedField] public ProtoId<DestinyDicePresetPrototype>? Preset;

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
    /// If defined, the amount of seconds before the die can trigger effects on roll again.
    [DataField, AutoNetworkedField] public float? RollDelay;

    /// The last person to roll the die and trigger an effect.
    [ViewVariables(VVAccess.ReadOnly)] public EntityUid? ActiveRoller;
    /// The last grid the die landed on when an effect was triggered.
    [ViewVariables(VVAccess.ReadOnly)] public EntityUid? ActiveGrid;
    /// The last grid the die roller was on when the die landed and an effect was triggered.
    [ViewVariables(VVAccess.ReadOnly)] public EntityUid? RollerGrid;

    /// The time at which effects are allowed to be triggered again. Determined via <see cref="RollDelay"/>.
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextAllowedRollTime;
    /// The time at which the next queued effect group will trigger.
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextEffectTriggerTime;

    /// Tracks if the die is actively triggering effects.
    [ViewVariables(VVAccess.ReadOnly)] public bool IsActive;

    /// Mirror of <see cref="Content.Shared.Dice.DiceComponent.CurrentValue"/> for faster lookup.
    [ViewVariables(VVAccess.ReadOnly)] public int CurrentValue;
    /// The previous value of the die.
    [ViewVariables(VVAccess.ReadOnly)] public int PreviousValue;

    /// A record of all effects that have been triggered with this roll and whether they successfully triggered or not.
    [ViewVariables(VVAccess.ReadOnly)] public Dictionary<DestinyDiceEffect, bool> EffectResults = [];
    /// Tracks the current active effect group.
    [ViewVariables(VVAccess.ReadOnly)] public DestinyDiceEffectGroup? CurrentEffectGroup;
    /// Tracks the current active effect.
    [ViewVariables(VVAccess.ReadOnly)] public DestinyDiceEffect? CurrentEffect;
    /// Tracks the current effect index in the active group.
    [ViewVariables(VVAccess.ReadOnly)] public int CurrentEffectIndex;
}
