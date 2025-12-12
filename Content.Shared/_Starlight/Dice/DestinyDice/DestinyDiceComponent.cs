using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Dice.DestinyDice;

[RegisterComponent, Robust.Shared.GameStates.NetworkedComponent]
public sealed partial class DestinyDiceComponent : Component
{
    /// <summary>
    /// Dictionary of target numbers and the effect groups for that number.
    /// </summary>
    [DataField] public List<DestinyDiceEffectGroup> EffectGroups = [];
    /// <summary>
    /// Message to show when a rolled number has no associated effect group or effects.
    /// </summary>
    [DataField] public string? NoEffectMessage;
    /// <summary>
    /// Message to show when the die is on cooldown. Only applicable when <see cref="RollDelay"/> is set.
    /// </summary>
    [DataField] public string? CooldownMessage;
    /// <summary>
    /// Message to show when trying to roll the die while effects are still running.
    /// </summary>
    [DataField] public string? BusyMessage;
    /// <summary>
    /// A default message to show when none is set for an effect group. Will show nothing if left unset.
    /// </summary>
    [DataField] public string? DefaultEffectMessage;
    /// <summary>
    /// How long in seconds you must wait before rolling the die works again.
    /// </summary>
    [DataField] public float RollDelay;
    /// <summary>
    /// The person who just threw the die.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public NetEntity? RollerEntity;
    /// <summary>
    /// The last person who threw the die, used to ensure that someone else can't just roll the die right after you and become the target.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public NetEntity? LastRoller;
    /// <summary>
    /// The grid the die just landed/was rolled on.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public NetEntity? RolledGrid;
    /// <summary>
    /// The time that the die will be off of cooldown.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextAllowedRollTime;
    /// <summary>
    /// The time that the next queued effect group will trigger.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextTriggerTime;
    /// <summary>
    /// Tracker to assist with logic flow.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public bool Active;
    /// <summary>
    /// The last value that was rolled, for proper logic flow.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public int LastValue;
}

public interface IDestinyDiceTargetable
{
    /// <summary>
    /// Whether to target the player, will target the die if false.
    /// </summary>
    public bool TargetPlayer { get; set; }
    /// <summary>
    /// Whether to target entities or not. <see cref="TargetPlayer"/> takes priority over this.
    /// </summary>
    public bool TargetEntity { get; set; }
    /// <summary>
    /// Whether to target multiple players, only checked if <see cref="TargetPlayer"/> is true.
    /// </summary>
    public bool TargetMultiple { get; set; }
    /// <summary>
    /// Whether to ignore entities with <see cref="Ghost.GhostComponent"/> or not.
    /// </summary>
    public bool AllowGhosts { get; set; }
    /// <summary>
    /// The range for finding targets if <see cref="TargetEntity"/> or <see cref="TargetPlayer"/> and <see cref="TargetMultiple"/> are true.
    /// </summary>
    public float Range { get; set; }
    /// <summary>
    /// The target prototype to try to find if <see cref="TargetEntity"/> is true.
    /// </summary>
    public EntProtoId TargetProto { get; set; }
}

#region Effects
/// <summary>
/// Groups of effects, multiple groups can be associated with a single number, and are picked via weight.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DestinyDiceEffectGroup
{
    /// <summary>
    /// Conditions that determine eligibility to be picked when rolling.
    /// </summary>
    [DataField] public List<IDestinyDiceRollCondition> RollConditions = [];
    
    /// <summary>
    /// The weight of the effect, in the event that multiple effects target the same value, one will be picked at random using the provided weight.
    /// Using a weight of -1 will signify that this effect should ALWAYS occur when rolled.
    /// </summary>
    [DataField] public float? Weight;
    
    /// <summary>
    /// The effects for this group
    /// </summary>
    [DataField] public List<IDestinyDiceEffect> Effects = [];
    
    /// <summary>
    /// Conditions that must pass for this effect group to execute. 
    /// </summary>
    [DataField] public List<IDestinyDiceTriggerCondition> TriggerConditions = [];
    
    /// <summary>
    /// The maximum amount of times this group can trigger for this destiny dice instance.
    /// </summary>
    [DataField] public int MaxTriggers;

    /// <summary>
    /// The popup message to show when this group is rolled and is out of triggers.
    /// </summary>
    [DataField] public string? OutOfTriggersMessage;

    /// <summary>
    /// An arbitrary delay before initiating the group. For suspense. :)
    /// </summary>
    [DataField] public float Delay;

    /// <summary>
    /// Message that shows when rolling this group and it is able to trigger.
    /// </summary>
    [DataField] public string? SuccessMessage;
    
    /// <summary>
    /// Message that shows when rolling this group and it fails to trigger.
    /// </summary>
    [DataField] public string? FailureMessage;
    
    /// <summary>
    /// The number of times this group has been triggered.
    /// </summary>
    [ViewVariables] public int TimesTriggered;
}

[NetSerializable, Serializable]
public record DestinyDiceEffectResult(bool Success);

#region Base Interface
/// <summary>
/// Base destiny dice effect interface.
/// </summary>
public interface IDestinyDiceEffect : IDestinyDiceTargetable
{
    /// <summary>
    /// Whether this effect targets the client system or not.
    /// </summary>
    public bool TargetClient { get; set; }
    /// <summary>
    /// A list of conditions that must pass for this effect to execute.
    /// If <see cref="IDestinyDiceTriggerCondition.RequiredToExecute"/> is false, only the current target loop will be skipped.
    /// If it is true, the entire effect will be skipped, returning as if it failed.
    /// </summary>
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <summary>
    /// Optional ID for tracking previous effects and whether they succeeded or not.
    /// </summary>
    public int? EffectID { get; set; }
    /// <summary>
    /// The maximum amount of times this effect can trigger for this destiny dice instance.
    /// </summary>
    public int MaxTriggers { get; set; }
    /// <summary>
    /// The number of times this effect has been triggered.
    /// </summary>
    public int TimesTriggered { get; set; }
    /// <summary>
    /// The popup message to show when this effect is rolled and is out of triggers.
    /// </summary>
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <summary>
    /// An arbitrary delay before initiating the effect. For suspense. :)
    /// </summary>
    public float Delay { get; set; }
    /// <summary>
    /// Effect IDs from the group this effect belongs to that need to have run successfully for this effect to trigger.
    /// </summary>
    public List<int>? DependsOn { get; set; }
    /// <summary>
    /// The popup message to show when this effect is rolled and successfully triggers.
    /// </summary>
    public string? SuccessMessage { get; set; }
    /// <summary>
    /// The popup message to show when this effect fails for whatever reason.
    /// </summary>
    public string? FailureMessage { get; set; }
}
#endregion

#region EmptyEffect
/// <summary>
/// Primarily for when you just need a popup message of some kind or some other kind of padding for some reason.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class EmptyEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
} 
#endregion

#region InjectReagentEffect
/// <summary>
/// Attempt to inject a reagent into a given solution on the target.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class InjectReagentEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }

    public required string SolutionName { get; set; }
    public required string ReagentProto { get; set; }
    public float Quantity { get; set; } = 10;
}
#endregion

#region SpawnPrototypeEffect
/// <summary>
/// Spawns a list of defined prototypes.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class SpawnPrototypeEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    
    /// <summary>
    /// The prototype(s) that should be spawned.
    /// </summary>
    public List<EntProtoId> Protos { get; set; } = [];
}
#endregion

#region DeletePrototypeEffect
/// <summary>
/// Deletes the target prototype.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class DeletePrototypeEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <summary>
    /// The prototype to delete, actually. This effect ignores the normal target stuff and just uses this to decide what to delete.
    /// </summary>
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
}
#endregion

#region RandomTeleportationEffect
/// <summary>
/// Teleports the target randomly based on a set of conditions.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class RandomTeleportationEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// range for teleport.
    /// </summary>
    public float TeleportationRange { get; set; } = 500;
    /// <summary>
    /// whether the target needs to stay on station, this is not the same as staying on the grid as multiple grids can belong to a station.
    /// </summary>
    public bool StayOnStation { get; set; }
    /// <summary>
    /// whether the target's destination is on the same grid or not
    /// </summary>
    public bool StayOnCurrentGrid { get; set; }
    /// <summary>
    /// allows target to end up in space, if false they will always end up on a grid.
    /// </summary>
    public bool AllowSpace { get; set; }
}
#endregion

#region SwapTeleportationEffect
/// <summary>
/// Swaps two targets with each other.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class SwapTeleportationEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    
    public bool SecondTargetPlayers { get; set; }
    public bool SecondTargetEntity { get; set; }
    public EntProtoId SecondTargetProto { get; set; }
    public float Range { get; set; }
}
#endregion

#region AddGameRuleEffect
/// <summary>
/// Adds a gamerule/event to the round.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class AddGameRuleEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// List of entity prototype ids, must correspond to an entity with a GameRuleComponent. entities without this will be skipped.
    /// </summary>
    public EntProtoId Proto { get; set; }
}
#endregion

#region SpawnGasMixtureEffect
/// <summary>
/// Spawns a gas mixture on the target.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class SpawnGasMixtureEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }

    public required Gas Gas { get; set; }

    public float Moles { get; set; } = Atmospherics.MolesCellStandard * 1.5f;

    public float Temperature { get; set; } = Atmospherics.T20C;

    public float Volume { get; set; } = 0.4f;
}
#endregion

#region KillTargetEffect
/// <summary>
/// Attempt to kill the target, if they cannot necessarily be put into a killed state, ash them instead.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class KillTargetEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
}
#endregion

#region ArmStationNukeEffect
/// <summary>
/// Arms the nuke of the station the die was rolled on if possible.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class ArmStationNukeEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
}
#endregion

#region CargoPurchaseEffect
/// <summary>
/// Purchases something from the cargo request computer, can specify whether to do this for free or to drain station budget.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class CargoPurchaseEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// the prototype to purchase
    /// </summary>
    public string? Product { get; set; }
    
    public string? Account { get; set; }
    
    /// <summary>
    /// Whether to charge the station budget or not.
    /// </summary>
    public bool IsFree { get; set; }

    public int Quantity { get; set; } = 1;
}
#endregion

#region ChangeScaleEffect
/// <summary>
/// Changes the scale of the target.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class ChangeScaleEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    
    public Vector2 Scale { get; set; }
}
#endregion

#region AddComponentEffect
/// <summary>
/// Adds a component to the target.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class AddComponentEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }

    public required string ComponentName { get; set; }
}
#endregion

#region ModifyComponentEffect
/// <summary>
/// Modifies the value of a component on the target.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class ModifyComponentEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    
    // the idea is to modify it via view variables. I'd use a component registry but those aren't serializable.
    public required string ComponentName { get; set; }
    public required string VariablePath { get; set; }
    public required string NewValue { get; set; }
}
#endregion

#region RemoveComponentEffect
/// <summary>
/// Removes a component from the target.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class RemoveComponentEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />    
    public float Range { get; set; }
    
    public required string ComponentName { get; set; }
}
#endregion

#region ExplosionEffect
/// <summary>
/// Spawn an explosion on the target. If the target is the die, the explosion will *not* set off another effect.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class ExplosionEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }

    public string TypeId { get; set; } = SharedExplosionSystem.DefaultExplosionPrototypeId.ToString();

    public float TotalIntensity { get; set; } = 200;

    public float Slope { get; set; } = 5;

    public float MaxIntensity { get; set; } = 100;

    public float TileBreakScale { get; set; } = 1;

    public int MaxTileBreak { get; set; } = 2147483647;

    public bool CanCreateVacuum { get; set; } = true;
}
#endregion

#region SendToChessDimensionEffect
/// <summary>
/// Sends the target to the chess dimension.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class SendToChessDimensionEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
}
#endregion

#region StationAnnouncementEffect
/// <summary>
/// Sends a station announcement. Is globally sent if <see cref="Global"/> is true.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class StationAnnouncementEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }

    public required string Message { get; set; }
    public string Sender { get; set; } = "Central Command";
    public string Color { get; set; } = "#ffff00";
    public SoundSpecifier Sound { get; set; } = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
    public bool Global { get; set; }
}
#endregion

#region ConvertToAntagonistEffect
/// <summary>
/// Attempts to convert the target into an antagonist.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class ConvertToAntagonistEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public bool TargetClient { get; set; }
    /// <inheritdoc />
    public List<IDestinyDiceTriggerCondition>? Conditions { get; set; }
    /// <inheritdoc />
    public int? EffectID { get; set; }
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public List<int>? DependsOn { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? FailureMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    public float Range { get; set; }
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// Antag prototype.
    /// </summary>
    public required string Proto { get; set; }
}
#endregion


#endregion

#region Conditions

#region Roll Conditions

#region Base Interface
/// <summary>
/// Condition to determine the roll needed for an effect group to be eligible for being picked  
/// </summary>
public interface IDestinyDiceRollCondition
{
}
#endregion

#region SideCondition
/// <summary>
/// Must land on this specific side
/// </summary>
public sealed class SideCondition : IDestinyDiceRollCondition
{
    public required int Value { get; set; }
}
#endregion

#region SideRangeCondition
/// <summary>
/// Must land within the specified values
/// </summary>
public sealed class SideRangeCondition : IDestinyDiceRollCondition
{
    public required int Min { get; set; }
    public required int Max { get; set; }
}
#endregion

#endregion

#region Trigger Conditions

#region Base Interface
/// <summary>
/// Base destiny die condition interface. Worth noting that when placed onto an effect, targets on the condition are ignored. as it will use the targets from the effect.
/// </summary>
public interface IDestinyDiceTriggerCondition : IDestinyDiceTargetable
{
    /// <summary>
    /// If on an effect, when true it will immediately return as a failure if a condition doesn't pass.
    /// If false, will allow the effect to execute, but skip any targets in which this condition would fail.
    /// <br/>Has no effect on groups as a condition failing on those will always fail the whole group instantly.
    /// </summary>
    public bool RequiredToExecute { get; set; }
    /// <summary>
    /// If true, flips the condition to pass where it would normally fail and vice versa.
    /// </summary>
    public bool FlipCondition { get; set; }
}
#endregion

#region ClothMuncherCondition
/// <summary>
/// Passes if the target is unable to consume normal food and can only eat cloth. (moths)
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class ClothMuncherCondition : IDestinyDiceTriggerCondition
{
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public bool RequiredToExecute { get; set; }
    /// <inheritdoc />
    public bool FlipCondition { get; set; }
}
#endregion

#region DamageTypeOverValueCondition
/// <summary>
/// Passes if the specified <see cref="Damage.Prototypes.DamageTypePrototype"/> on the target is over a certain value.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class DamageTypeOverValueCondition : IDestinyDiceTriggerCondition
{
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public bool RequiredToExecute { get; set; }
    /// <inheritdoc />
    public bool FlipCondition { get; set; }

    /// <summary>
    /// The <see cref="Damage.Prototypes.DamageTypePrototype"/> to check for.
    /// </summary>
    public required string Type { get; set; }
    /// <summary>
    /// The damage value that must be hit or exceeded in order to pass.
    /// </summary>
    public required float TargetValue { get; set; }
}
#endregion

#region DamageGroupOverValueCondition
/// <summary>
/// Passes if the specified <see cref="Damage.Prototypes.DamageGroupPrototype"/> on the target is over a certain value.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class DamageGroupOverValueCondition : IDestinyDiceTriggerCondition
{
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public bool RequiredToExecute { get; set; }
    /// <inheritdoc />
    public bool FlipCondition { get; set; }

    /// <summary>
    /// The <see cref="Damage.Prototypes.DamageGroupPrototype"/> to check for.
    /// </summary>
    public required string Group { get; set; }
    /// <summary>
    /// The damage value that must be hit or exceeded in order to pass.
    /// </summary>
    public required float TargetValue { get; set; }
}
#endregion

#region TotalDamageOverValueCondition
/// <summary>
/// Passes if the total damage on the target is over a certain value.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class TotalDamageOverValueCondition : IDestinyDiceTriggerCondition
{
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public bool RequiredToExecute { get; set; }
    /// <inheritdoc />
    public bool FlipCondition { get; set; }

    /// <summary>
    /// The damage value that must be hit or exceeded in order to pass.
    /// </summary>
    public required float TargetValue { get; set; }
}
#endregion

#region DamageableCondition
/// <summary>
/// Passes if the target has a <see cref="Damage.DamageableComponent"/>.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class DamageableCondition : IDestinyDiceTriggerCondition
{
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public bool RequiredToExecute { get; set; }
    /// <inheritdoc />
    public bool FlipCondition { get; set; }
}
#endregion

#region IsMobStateCondition
/// <summary>
/// Passes if the target has a <see cref="Mobs.Components.MobStateComponent"/>, and if their <see cref="Mobs.Components.MobStateComponent.CurrentState"/> is set to the specified <see cref="Mobs.MobState"/>.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed class IsMobStateCondition : IDestinyDiceTriggerCondition
{
    /// <inheritdoc />
    public bool TargetPlayer { get; set; }
    /// <inheritdoc />
    public bool TargetEntity { get; set; }
    /// <inheritdoc />
    public bool TargetMultiple { get; set; }
    /// <inheritdoc />
    public bool AllowGhosts { get; set; }
    /// <inheritdoc />
    public float Range { get; set; }
    /// <inheritdoc />
    public EntProtoId TargetProto { get; set; }
    /// <inheritdoc />
    public bool RequiredToExecute { get; set; }
    /// <inheritdoc />
    public bool FlipCondition { get; set; }
    
    /// <summary>
    /// The state required to pass the check.
    /// </summary>
    public MobState TargetState { get; set; }
}
#endregion

#endregion

#endregion