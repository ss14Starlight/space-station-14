using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Explosion.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Dice.DestinyDice;

[RegisterComponent, Robust.Shared.GameStates.NetworkedComponent]
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
    /// <summary>
    /// The last entity to throw the die
    /// </summary>
    [ViewVariables] public NetEntity? RollerEntity;
    /// <summary>
    /// The grid the die just landed/was rolled on.
    /// </summary>
    [ViewVariables] public NetEntity? RolledGrid;
    [ViewVariables] public TimeSpan NextTriggerTime;
    [ViewVariables] public TimeSpan NextAllowedRollTime;
    [ViewVariables] public bool Active;
    [ViewVariables] public int LastValue;
    [ViewVariables] public NetEntity? LastRoller;
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
    
    public bool TargetPlayer { get; set; }
    
    public bool TargetEntity { get; set; }
    
    public bool TargetMultiple { get; set; }
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
    
    public EntProtoId TargetProto { get; set; }
}

[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }

    public bool TargetMultiple { get; set; }
    
    /// <summary>
    /// Only valid if SpawnOnPlayer and SpawnOnMultiple is true, the range in which to check for players. -1 = all players on the map, Infinity = all players on any map.
    /// </summary>
    public float Range { get; set; }

    public EntProtoId TargetProto { get; set; }
}

/// <summary>
/// Deletes the target prototype
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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

    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }

    /// <summary>
    /// The prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }
    
    /// <summary>
    /// Range to check for, -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// Teleports an entity to a random place
/// Can target the roller or the die.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public float Range { get; set; }
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

[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public EntProtoId TargetProto { get; set; }
    public bool SecondTargetPlayers { get; set; }
    public bool SecondTargetEntity { get; set; }
    public EntProtoId SecondTargetProto { get; set; }
    public float Range { get; set; }
}

/// <summary>
/// Adds gamerule(s) to the round.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public float Range { get; set; }
    public EntProtoId TargetProto { get; set; }

    /// <summary>
    /// List of entity prototype ids, must correspond to an entity with a GameRuleComponent. entities without this will be skipped.
    /// </summary>
    public EntProtoId Proto { get; set; } = default!;
}

/// <summary>
/// Spawns a gas mixture. Can target the die or the roller.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public float Range { get; set; }
    public EntProtoId TargetProto { get; set; }

    public required Gas Gas { get; set; }

    public float Moles { get; set; } = Atmospherics.MolesCellStandard * 1.5f;

    public float Temperature { get; set; } = Atmospherics.T20C;

    public float Volume { get; set; } = 0.4f;
}

/// <summary>
/// Simply kills the target entity. If it can be killed, it will kill them, if not then it will delete them.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
public sealed class KillTargetEffect : IDestinyDiceEffect
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public float Range { get; set; }
    public EntProtoId TargetProto { get; set; }
}

/// <summary>
/// Arms the nuke of the station the die was rolled on.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public float Range { get; set; }
    public EntProtoId TargetProto { get; set; }
}

/// <summary>
/// Purchases something from the cargo request computer, can specify whether to do this for free or to drain station budget.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public float Range { get; set; }
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

/// <summary>
/// Changes the scale of either the roller, the die, or nearby entities of a given prototype.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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

    public bool TargetEntity { get; set; }

    public bool TargetMultiple { get; set; }

    /// <summary>
    /// Prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }
    
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
    
    public Vector2 Scale { get; set; }
}

/// <summary>
/// Adds a component to the roller or nearby entities of a given prototype.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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

    public bool TargetEntity { get; set; }

    public bool TargetMultiple { get; set; }

    /// <summary>
    /// Prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }
    
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// Modifies a component on the roller.
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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

    public bool TargetEntity { get; set; }

    public bool TargetMultiple { get; set; }

    /// <summary>
    /// Prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }
    
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// Removes a component from the roller or nearby entities of a given type
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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

    public bool TargetEntity { get; set; }

    public bool TargetMultiple { get; set; }

    /// <summary>
    /// Prototype to target
    /// </summary>
    public EntProtoId TargetProto { get; set; }
    
    /// <summary>
    /// Range to check for the target. -1 = all on map, Infinity = all on server.
    /// </summary>
    public float Range { get; set; }
}

/// <summary>
/// It go boom
/// </summary>
[DataRecord] [Serializable, NetSerializable]
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

    public bool TargetEntity { get; set; }

    public bool TargetMultiple { get; set; }
    
    public float Range { get; set; }
    public EntProtoId TargetProto { get; set; }

    public string TypeId { get; set; } = SharedExplosionSystem.DefaultExplosionPrototypeId.ToString();

    public float TotalIntensity { get; set; } = 200;

    public float Slope { get; set; } = 5;

    public float MaxIntensity { get; set; } = 100;

    public float TileBreakScale { get; set; } = 1;

    public int MaxTileBreak { get; set; } = 2147483647;

    public bool CanCreateVacuum { get; set; } = true;
}

[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public float Range { get; set; }
    public EntProtoId TargetProto { get; set; }
}

[DataRecord] [Serializable, NetSerializable]
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
    
    public bool TargetPlayer { get; set; }
    public bool TargetEntity { get; set; }
    public bool TargetMultiple { get; set; }
    public float Range { get; set; }
    public EntProtoId TargetProto { get; set; }

    public required string Message { get; set; }
    public string Sender { get; set; } = "Central Command";
    public string Color { get; set; } = "#ffff00";
    public SoundSpecifier Sound { get; set; } = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
    public bool Global { get; set; }
}

#endregion