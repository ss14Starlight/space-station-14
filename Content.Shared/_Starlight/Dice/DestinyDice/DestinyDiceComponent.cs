using Content.Shared.Cargo.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Admeme.DestinyDice;

[RegisterComponent]
public sealed partial class DestinyDiceComponent : Component
{
    [DataField] public Dictionary<int, List<DestinyDiceEffectGroup>> EffectGroups = [];
    [DataField] public string? NoEffectMessage;
    [DataField] public string? CooldownMessage;
    [DataField] public string? DefaultEffectMessage;
    /// <summary>
    /// how long before the die works again
    /// </summary>
    [DataField] public float RollDelay;
    [ViewVariables] public EntityUid RollerEntity;
    [ViewVariables] public TimeSpan NextTriggerTime;
    [ViewVariables] public TimeSpan NextAllowedRollTime;
    [ViewVariables] public bool Active;
    [ViewVariables] public int LastValue;
}

[DataDefinition]
[UsedImplicitly]
public sealed partial class DestinyDiceEffectGroup
{
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
    /// The number of times this group has been triggered.
    /// </summary>
    [ViewVariables] public int TimesTriggered;
}

#region Effects

public interface IDestinyDiceEffect
{
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
    /// The popup message to show when this effect is rolled and successfully triggers.
    /// </summary>
    public string? SuccessMessage { get; set; }
    
    /// <summary>
    /// Whether to show effect messages or not in general.
    /// </summary>
    public bool ShowEffectMessages { get; set; }
}

[DataRecord]
public sealed class SpawnPrototypeEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }

    /// <summary>
    /// The prototype(s) that should be spawned.
    /// </summary>
    public List<EntProtoId> Protos { get; set; } = [];
    
    /// <summary>
    /// Whether to have the spawn origin be the player or the die.
    /// </summary>
    public bool SpawnOnPlayer { get; set; }
    
    /// <summary>
    /// Only valid if SpawnOnPlayer is true, try to spawn on multiple players instead of just the player who rolled.
    /// </summary>
    public bool SpawnOnMultiple { get; set; }
    
    /// <summary>
    /// Only valid if SpawnOnPlayer and SpawnOnMultiple is true, the range in which to check for players. -1 = all players on the map, Infinity = all players on any map.
    /// </summary>
    public float PlayerRange { get; set; }
}

/// <summary>
/// Outright replaces the target entity with a new one.
/// </summary>
[DataRecord]
public sealed class TransmutationEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// The prototype to target.
    /// </summary>
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// A list of components the target prototype must have to be eligible.
    /// </summary>
    public ComponentRegistry ComponentFilter { get; set; } = default!;

    /// <summary>
    /// Maximum distance to check from the die for target entities.
    /// -1 signifies all valid targets on the current map, and Infinity is all valid targets regardless of map.
    /// </summary>
    public float Range { get; set; }
    
    /// <summary>
    /// The prototype that the target entity should transform into.
    /// </summary>
    public EntProtoId ResultProto { get; set; }

    /// <summary>
    /// Any components to attach to the resulting entity.
    /// </summary>
    public ComponentRegistry ResultComponentOverrides { get; set; } = default!;
    
    /// <summary>
    /// Whether to transfer the mind of the target entity to the new entity, if it has one.
    /// </summary>
    public bool TransferMind { get; set; }

    /// <summary>
    /// Whether to rename the result entity to that of the target entity or not.
    /// </summary>
    public bool TransferName { get; set; }
}

/// <summary>
/// Deletes the target prototype
/// </summary>
[DataRecord]
public sealed class DeletePrototypeEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// The prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }
    
    /// <summary>
    /// A list of components to filter by
    /// </summary>
    public ComponentRegistry ComponentFilter { get; set; } = default!;
    
    /// <summary>
    /// Range to check for, -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// Teleports an entity to a random place
/// Can target the roller or the die.
/// </summary>
[DataRecord]
public sealed class RandomTeleportationEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// If true, target the player who rolled. If false, the die is the target.
    /// </summary>
    public bool TargetPlayer { get; set; }
}

[DataRecord]
public sealed class SwapTeleportationEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
}

/// <summary>
/// Adds gamerule(s) to the round.
/// </summary>
[DataRecord]
public sealed class AddGameRuleEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// List of entity prototype ids, must correspond to an entity with a GameRuleComponent. entities without this will be skipped.
    /// </summary>
    public EntProtoId Proto { get; set; } = default!;
}

/// <summary>
/// Spawns a gas mixture. Can target the die or the roller.
/// </summary>
[DataRecord]
public sealed class SpawnGasMixtureEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
}

/// <summary>
/// Simply kills the entity that rolled the die. If it can be killed, it will kill them, if not then it will delete them.
/// </summary>
[DataRecord]
public sealed class KillRollerEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
}

/// <summary>
/// Arms the nuke of the station the die was rolled on.
/// </summary>
[DataRecord]
public sealed class ArmStationNukeEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
}

/// <summary>
/// Purchases something from the cargo request computer, can specify whether to do this for free or to drain station budget.
/// </summary>
[DataRecord]
public sealed class CargoPurchaseEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// the prototype to purchase
    /// </summary>
    public CargoProductPrototype ProductPrototype { get; set; } = default!;
    
    /// <summary>
    /// Whether to charge the station budget or not.
    /// </summary>
    public bool IsFree { get; set; }
}

/// <summary>
/// Changes the scale of either the roller, the die, or nearby entities of a given prototype.
/// </summary>
[DataRecord]
public sealed class ChangeScaleEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// Simply target the one who rolled
    /// </summary>
    public bool TargetPlayer { get; set; }

    /// <summary>
    /// Prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// A list of components the target prototype must have to be eligible.
    /// </summary>
    public ComponentRegistry ComponentFilter { get; set; } = default!;
    
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// Adds a component to the roller or nearby entities of a given prototype.
/// </summary>
[DataRecord]
public sealed class AddComponentEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// Simply target the one who rolled
    /// </summary>
    public bool TargetPlayer { get; set; }

    /// <summary>
    /// Prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// A list of components the target prototype must have to be eligible.
    /// </summary>
    public ComponentRegistry ComponentFilter { get; set; } = default!;
    
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// Modifies a component on the roller.
/// </summary>
[DataRecord]
public sealed class ModifyComponentEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// Simply target the one who rolled
    /// </summary>
    public bool TargetPlayer { get; set; }

    /// <summary>
    /// Prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// A list of components the target prototype must have to be eligible.
    /// </summary>
    public ComponentRegistry ComponentFilter { get; set; } = default!;
    
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// Removes a component from the roller or nearby entities of a given type
/// </summary>
[DataRecord]
public sealed class RemoveComponentEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }

    /// <summary>
    /// Simply target the one who rolled
    /// </summary>
    public bool TargetPlayer { get; set; }

    /// <summary>
    /// Prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// A list of components the target prototype must have to be eligible.
    /// </summary>
    public ComponentRegistry ComponentFilter { get; set; } = default!;
    
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// It go boom
/// </summary>
[DataRecord]
public sealed class ExplosionEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    /// <summary>
    /// Target the player, if not then explode on the die.
    /// </summary>
    public bool TargetPlayer { get; set; }
}

[DataRecord]
public sealed class SendToChessDimensionEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
}

[DataRecord]
public sealed class StationAnnouncementEffect : IDestinyDiceEffect
{
    /// <inheritdoc />
    public int MaxTriggers { get; set; }
    /// <inheritdoc />
    public int TimesTriggered { get; set; }
    /// <inheritdoc />
    public float Delay { get; set; }
    /// <inheritdoc />
    public string? SuccessMessage { get; set; }
    /// <inheritdoc />
    public string? EffectOutOfTriggersMessage { get; set; }
    /// <inheritdoc />
    public bool ShowEffectMessages { get; set; }
    
    public required string Message { get; set; }
    public string Sender { get; set; } = "Central Command";
    public string Color { get; set; } = "#ffff00";
    public SoundSpecifier Sound { get; set; } = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
}



#endregion