using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.DestinyDice;

public enum DestinyDiceTargetType : byte
{
    None, // No specific target.
    Self, // Targets the die.
    Roller, // Targets the active roller of the die.
    Filter // Target based on several filters such as range, ghosts, actors, etc.
}

public enum DestinyDiceGridFilter : byte
{
    None,
    SameGrid,
    OtherGrids,
    NoGrid,
}

/*
 * Effects and groups need to be cloneable due to preset prototypes existing.
 * Since they use these as properties to save me having to redefine a bunch of stuff, all instances of Destiny Dice
 * that use a given preset will all have a reference to the same instance of a group/effect. So they need to be cloned
 * in order to prevent something like TimesTriggered or TimesRolled from affecting *all* dice with the preset.
 */

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DestinyDiceEffectGroup : ICloneable
{
    /// List of roll data that act as target values where this group can trigger.
    [DataField("targetRolls", required: true)] public List<DestinyDiceRollData> RollData = [];
    /// List of conditions that must pass in order for this group to trigger its effects.
    [DataField] public List<EntityCondition> Conditions = [];
    /// Determines if just any conditions needs to pass or if all conditions must pass in order to trigger this group.
    [DataField] public bool AllConditionsMustPass;
    /// List of effects that this group will trigger in sequence if all conditions and probability pass.
    [DataField] public List<DestinyDiceEffect> Effects = [];
    /// Weight value used when picking groups that have <see cref="RollData"/> matching the rolled value.
    [DataField] public float? Weight;
    /// Probability that this group will trigger at all even if picked and all conditions pass.
    [DataField("prob")] public float Probability = 1;
    /// An arbitrary delay before effects in the group will begin getting triggered. For suspense. :)
    [DataField] public float Delay;

    /// <summary>
    /// The maximum number of times this group can be rolled and picked.
    /// Unlike <see cref="MaxTriggers"/>, this includes every time it is picked and fails to pass checks.
    /// </summary>
    [DataField] public int MaxRolls = -1;
    /// The maximum number of times this group can be triggered. A value of -1 means it can trigger infinitely.
    [DataField] public int MaxTriggers = -1;
    /// The number of times this group has been rolled and picked.
    [ViewVariables] public int TimesRolled;
    /// The number of times this group has been triggered.
    [ViewVariables] public int TimesTriggered;

    /// Message that pops up when this group is successfully triggered.
    [DataField] public string? SuccessMessage;
    /// Popup type for success message.
    [DataField] public PopupType SuccessPopupType = PopupType.Small;
    /// Message that pops up when this group is picked but fails a condition, and thus does not trigger.
    [DataField] public string? FailureMessage;
    /// Popup type for failure message.
    [DataField] public PopupType FailurePopupType = PopupType.Small;
    /// <summary>
    /// Message that pops up when <see cref="MaxTriggers"/> or <see cref="MaxRolls"/> is above -1
    /// and <see cref="TimesTriggered"/> or <see cref="TimesRolled"/> matches their respective max value, and thus does not trigger.
    /// </summary>
    [DataField] public string? ExhaustedMessage;
    /// Popup type for exhaust message.
    [DataField] public PopupType ExhaustedPopupType = PopupType.Small;

    public object Clone()
    {
        var group = new DestinyDiceEffectGroup
        {
            RollData = RollData,
            Conditions = Conditions,
            AllConditionsMustPass = AllConditionsMustPass,
            Weight = Weight,
            Probability = Probability,
            Delay = Delay,
            MaxRolls = MaxRolls,
            MaxTriggers = MaxTriggers,
            TimesRolled = TimesRolled,
            TimesTriggered = TimesTriggered,
            SuccessMessage = SuccessMessage,
            FailureMessage = FailureMessage,
            ExhaustedMessage = ExhaustedMessage
        };
        // clone these too
        foreach (var effect in Effects)
            group.Effects.Add((DestinyDiceEffect)effect.Clone());
        return group;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DestinyDiceEffect : ICloneable
{
    /// The <see cref="Content.Shared.EntityEffects.EntityEffect"/> to apply (conditions are defined here too).
    [DataField(required: true)] public EntityEffect? EntityEffect;
    /// The target data for this effect.
    [DataField] public DestinyDiceTargetData TargetData = new();
    /// Determines if just any conditions needs to pass or if all conditions must pass in order to trigger this effect.
    [DataField] public bool AllConditionsMustPass;
    /// Effect IDs that must have successfully triggered for this effect to trigger. Obviously the provided ID must come before this effect.
    [DataField] public List<string> DependsOnIds = [];
    /// Probability that this effect will trigger at all even if picked and all conditions pass.
    [DataField("prob")] public float Probability = 1;
    /// The ID of this effect, for other effects to check the success status of.
    [DataField] public string? EffectId;
    /// The delay before the NEXT effect in the group will trigger.
    [DataField] public float Delay;
    /// <summary>
    /// Signifies that this effect is required to pass checks and trigger. If <see langword="true"/>, and this effect
    /// fails to trigger for any reason, the group will prematurely finish triggering effects.
    /// </summary>
    [DataField] public bool RequiredTrigger;
    /// <summary>
    /// When <see langword="true"/>, instead of relying on <see cref="Delay"/>, process the next event upon
    /// the <see cref="DestinyDiceEffectEndEvent"/> event being raised on the die.
    /// </summary>
    [DataField] public bool EndOnEvent;

    // Yes I am aware that "rolls" isn't really the correct term here, but I don't care, sorry.
    /// <summary>
    /// The maximum number of times this effect can be rolled and picked.
    /// Unlike <see cref="MaxTriggers"/>, this includes every time it is picked and fails to pass checks.
    /// </summary>
    [DataField] public int MaxRolls = -1;
    /// The maximum number of times this effect can be triggered. A value of -1 means it can trigger infinitely.
    [DataField] public int MaxTriggers = -1;
    /// The number of times this effect has been rolled and picked.
    [DataField] public int TimesRolled;
    /// The number of times this effect has been triggered.
    [DataField] public int TimesTriggered;

    /// Message that pops up when this effect is successfully triggered.
    [DataField] public string? SuccessMessage;
    /// Popup type for success message.
    [DataField] public PopupType SuccessPopupType = PopupType.Small;
    /// Message that pops up when this effect is picked but fails a condition, and thus does not trigger.
    [DataField] public string? FailureMessage;
    /// Popup type for failure message.
    [DataField] public PopupType FailurePopupType = PopupType.Small;
    /// <summary>
    /// Message that pops up when <see cref="MaxTriggers"/> or <see cref="MaxRolls"/> is above -1
    /// and <see cref="TimesTriggered"/> or <see cref="TimesRolled"/> matches their respective max value, and thus does not trigger.
    /// </summary>
    [DataField] public string? ExhaustedMessage;
    /// Popup type for exhaust message.
    [DataField] public PopupType ExhaustedPopupType = PopupType.Small;

    public object Clone() =>
        new DestinyDiceEffect
        {
            TargetData = TargetData,
            EntityEffect = EntityEffect,
            AllConditionsMustPass = AllConditionsMustPass,
            DependsOnIds = DependsOnIds,
            Probability = Probability,
            EffectId = EffectId,
            Delay = Delay,
            RequiredTrigger = RequiredTrigger,
            MaxRolls = MaxRolls,
            MaxTriggers = MaxTriggers,
            TimesRolled = TimesRolled,
            TimesTriggered = TimesTriggered,
            SuccessMessage = SuccessMessage,
            FailureMessage = FailureMessage,
            ExhaustedMessage = ExhaustedMessage
        };
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DestinyDiceTargetData
{
    /// Determines what the effect will target.
    [DataField("type")] public DestinyDiceTargetType TargetType = DestinyDiceTargetType.None;
    /// Quick way to determine whether ghosts can be targeted or not.
    [DataField] public bool AllowGhosts;
    /// Quick way to determine whether targets must be controlled by a player or not.
    [DataField] public bool ActorControlled;
    /// <summary>
    /// The range to check for targets if <see cref="TargetType"/> is <see cref="DestinyDiceTargetType.Nearby"/>,
    /// <see cref="DestinyDiceTargetType.NearbyWhitelist"/>, or <see cref="DestinyDiceTargetType.NearbyPrototype"/>.
    /// </summary>
    [DataField] public float? Range;
    /// <summary>
    /// The entity prototype ID to target if <see cref="TargetType"/> is <see cref="DestinyDiceTargetType.Prototype"/>
    /// or <see cref="DestinyDiceTargetType.NearbyPrototype"/>.
    /// </summary>
    [DataField] public EntProtoId? TargetPrototypeId;
    /// <summary>
    /// The whitelist to use when <see cref="TargetType"/> is <see cref="DestinyDiceTargetType.Whitelist"/>
    /// or <see cref="DestinyDiceTargetType.NearbyWhitelist"/>.
    /// </summary>
    [DataField] public EntityWhitelist? Whitelist;
    /// Limit targets to those on the current map.
    [DataField] public bool SameMap;
    /// Limit targets by the given grid filter.
    [DataField] public DestinyDiceGridFilter GridFilter = DestinyDiceGridFilter.None;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DestinyDiceRollData
{
    /// Set if you want it to trigger on a single specific value. Will ignore <see cref="MinValue"/> and <see cref="MaxValue"/>.
    [DataField] public int? TargetValue;
    /// Set if you want a range of values, to set the minimum value. If set, <see cref="MaxValue"/> must be set too.
    [DataField] public int? MinValue;
    /// Set if you want a range of values, to set the maximum value. If set, <see cref="MinValue"/> must be set too.
    [DataField] public int? MaxValue;
}
