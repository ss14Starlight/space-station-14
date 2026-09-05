using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Starlight.DestinyDice;

// Hello, do you like duplicated code? I LOVE duplicated code. Here is a lot of duplicated code for the purpose of having inheritable prototypes.

[Prototype]
public sealed partial class DestinyDicePresetPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<DestinyDicePresetPrototype>))]
    public string[]? Parents { get; set; } = [];

    [AbstractDataField] public bool Abstract { get; set; }

    /// List of effect groups that will be used on component startup.
    [DataField] public List<ProtoId<DestinyDiceEffectGroupPrototype>> Groups { get; set; } = [];
}

[Prototype]
public sealed partial class DestinyDiceEffectGroupPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<DestinyDiceEffectGroupPrototype>))]
    public string[]? Parents { get; set; } = [];

    [AbstractDataField] public bool Abstract { get; set; }

    /// List of roll data that act as target values where this group can trigger.
    [DataField("targetRolls", required: true)] public List<DestinyDiceRollData> RollData = [];
    /// List of conditions that must pass in order for this group to trigger its effects.
    [DataField] public List<EntityCondition> Conditions = [];
    /// Determines if just any conditions needs to pass or if all conditions must pass in order to trigger this group.
    [DataField] public bool AllConditionsMustPass;
    /// List of effects that this group will trigger in sequence if all conditions and probability pass.
    [DataField] public List<ProtoId<DestinyDiceEffectPrototype>> Effects = [];
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
}

[Prototype]
public sealed partial class DestinyDiceEffectPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<DestinyDiceEffectPrototype>))]
    public string[]? Parents { get; set; } = [];

    [AbstractDataField] public bool Abstract { get; set; }

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
}
