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
    /// <summary>
    /// List of roll data that act as target values where this group can trigger.
    /// </summary>
    [DataField("targetRolls", required: true)] public List<DestinyDiceRollData> RollData = [];
    /// <summary>
    /// List of conditions that must pass in order for this group to trigger its effects.
    /// </summary>
    [DataField] public List<EntityCondition> Conditions = [];
    /// <summary>
    /// Determines if just any conditions needs to pass or if all conditions must pass in order to trigger this group.
    /// </summary>
    [DataField] public bool AllConditionsMustPass;
    /// <summary>
    /// List of effects that this group will trigger in sequence if all conditions and probability pass.
    /// </summary>
    [DataField] public List<DestinyDiceEffect> Effects = [];
    /// <summary>
    /// Weight value used when picking groups that have <see cref="RollData"/> matching the rolled value.
    /// </summary>
    [DataField] public float? Weight;
    /// <summary>
    /// Probability that this group will trigger at all even if picked and all conditions pass.
    /// </summary>
    [DataField("prob")] public float Probability = 1;
    /// <summary>
    /// An arbitrary delay before effects in the group will begin getting triggered. For suspense. :)
    /// </summary>
    [DataField] public float Delay;

    /// <summary>
    /// The maximum number of times this group can be rolled and picked.
    /// Unlike <see cref="MaxTriggers"/>, this includes every time it is picked and fails to pass checks.
    /// </summary>
    [DataField] public int MaxRolls = -1;
    /// <summary>
    /// The maximum number of times this group can be triggered. A value of -1 means it can trigger infinitely.
    /// </summary>
    [DataField] public int MaxTriggers = -1;
    /// <summary>
    /// The number of times this group starts off as having been rolled and picked. Live counts are tracked on <see cref="DestinyDiceComponent"/>.
    /// </summary>
    [ViewVariables] public int TimesRolled;
    /// <summary>
    /// The number of times this group starts off as having been triggered. Live counts are tracked on <see cref="DestinyDiceComponent"/>.
    /// </summary>
    [ViewVariables] public int TimesTriggered;

    /// <summary>
    /// Message that pops up when this group is successfully triggered.
    /// </summary>
    [DataField] public string? SuccessMessage;
    /// <summary>
    /// Popup type for success message.
    /// </summary>
    [DataField] public PopupType SuccessPopupType = PopupType.Small;
    /// <summary>
    /// Message that pops up when this group is picked but fails a condition, and thus does not trigger.
    /// </summary>
    [DataField] public string? FailureMessage;
    /// <summary>
    /// Popup type for failure message.
    /// </summary>
    [DataField] public PopupType FailurePopupType = PopupType.Small;
    /// <summary>
    /// Message that pops up when <see cref="MaxTriggers"/> or <see cref="MaxRolls"/> is above -1
    /// and <see cref="TimesTriggered"/> or <see cref="TimesRolled"/> matches their respective max value, and thus does not trigger.
    /// </summary>
    [DataField] public string? ExhaustedMessage;
    /// <summary>
    /// Popup type for exhaust message.
    /// </summary>
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
    /// <summary>
    /// The <see cref="Content.Shared.EntityEffects.EntityEffect"/> to apply (conditions are defined here too).
    /// </summary>
    [DataField(required: true)] public EntityEffect? EntityEffect;

    /// <summary>
    /// The target data for this effect.
    /// </summary>
    [DataField] public DestinyDiceTargetData TargetData = new();

    /// <summary>
    /// Determines if just any conditions needs to pass or if all conditions must pass in order to trigger this effect.
    /// </summary>
    [DataField] public bool AllConditionsMustPass;

    /// <summary>
    /// Effect IDs that must have successfully triggered for this effect to trigger. Obviously the provided ID must come before this effect.
    /// </summary>
    [DataField] public List<string> DependsOnIds = [];

    /// <summary>
    /// Probability that this effect will trigger at all even if picked and all conditions pass.
    /// </summary>
    [DataField("prob")] public float Probability = 1;

    /// <summary>
    /// The ID of this effect, for other effects to check the success status of.
    /// </summary>
    [DataField] public string? EffectId;

    /// <summary>
    /// The delay before the NEXT effect in the group will trigger.
    /// </summary>
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

    /// <summary>
    /// The maximum number of times this effect can be triggered. A value of -1 means it can trigger infinitely.
    /// </summary>
    [DataField] public int MaxTriggers = -1;

    /// <summary>
    /// The number of times this effect starts off as having been rolled and picked. Live counts are tracked on <see cref="DestinyDiceComponent"/>.
    /// </summary>
    [DataField] public int TimesRolled;

    /// <summary>
    /// The number of times this effect starts off as having been triggered. Live counts are tracked on <see cref="DestinyDiceComponent"/>.
    /// </summary>
    [DataField] public int TimesTriggered;

    /// <summary>
    /// Message that pops up when this effect is successfully triggered.
    /// </summary>
    [DataField] public string? SuccessMessage;

    /// <summary>
    /// Popup type for success message.
    /// </summary>
    [DataField] public PopupType SuccessPopupType = PopupType.Small;

    /// <summary>
    /// Message that pops up when this effect is picked but fails a condition, and thus does not trigger.
    /// </summary>
    [DataField] public string? FailureMessage;

    /// <summary>
    /// Popup type for failure message.
    /// </summary>
    [DataField] public PopupType FailurePopupType = PopupType.Small;

    /// <summary>
    /// Message that pops up when <see cref="MaxTriggers"/> or <see cref="MaxRolls"/> is above -1
    /// and <see cref="TimesTriggered"/> or <see cref="TimesRolled"/> matches their respective max value, and thus does not trigger.
    /// </summary>
    [DataField] public string? ExhaustedMessage;

    /// <summary>
    /// Popup type for exhaust message.
    /// </summary>
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
    /// <summary>
    /// Determines what the effect will target.
    /// </summary>
    [DataField("type")] public DestinyDiceTargetType TargetType = DestinyDiceTargetType.None;

    /// <summary>
    /// Quick way to determine whether ghosts can be targeted or not.
    /// </summary>
    [DataField] public bool AllowGhosts;

    /// <summary>
    /// Quick way to determine whether targets must be controlled by a player or not.
    /// </summary>
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

    /// <summary>
    /// Limit targets to those on the current map.
    /// </summary>
    [DataField] public bool SameMap;

    /// <summary>
    /// Limit targets by the given grid filter.
    /// </summary>
    [DataField] public DestinyDiceGridFilter GridFilter = DestinyDiceGridFilter.None;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DestinyDiceRollData
{
    /// <summary>
    /// Set if you want it to trigger on a single specific value. Will ignore <see cref="MinValue"/> and <see cref="MaxValue"/>.
    /// </summary>
    [DataField] public int? TargetValue;

    /// <summary>
    /// Set if you want a range of values, to set the minimum value. If set, <see cref="MaxValue"/> must be set too.
    /// </summary>
    [DataField] public int? MinValue;

    /// <summary>
    /// Set if you want a range of values, to set the maximum value. If set, <see cref="MinValue"/> must be set too.
    /// </summary>
    [DataField] public int? MaxValue;
}
