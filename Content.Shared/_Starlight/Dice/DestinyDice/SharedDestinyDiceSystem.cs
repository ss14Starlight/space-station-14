using System.Linq;
using Content.Shared._Starlight.Dice;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Dice;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Dice.DestinyDice;

public abstract class SharedDestinyDiceSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager _proto = default!;
    [Dependency] protected readonly IGameTiming _timing = default!;
    [Dependency] protected readonly IComponentFactory _factory = default!;
    [Dependency] protected readonly IRobustRandom _sharedRandom = default!;
    [Dependency] protected readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] protected readonly SharedTransformSystem _transform = default!;
    [Dependency] protected readonly SharedMapSystem _map = default!;
    [Dependency] protected readonly SharedHandsSystem _hands = default!;
    [Dependency] protected readonly SharedContainerSystem _container = default!;
    [Dependency] protected readonly SharedPopupSystem _popup = default!;
    [Dependency] protected readonly SharedGameTicker _ticker = default!;
    [Dependency] protected readonly SharedVerbSystem _verb = default!;
    [Dependency] protected readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StomachSystem _stomach = default!;
    
    protected static readonly ProtoId<DamageTypePrototype> _bluntDamageType = "Blunt";
    private static readonly string _clothTag = "ClothMade";
    
    /// <summary>
    /// Return status of effects with IDs. An effect is required to have an ID to track this properly.
    /// </summary>
    protected readonly Dictionary<NetEntity, Dictionary<IDestinyDiceEffect, DestinyDiceEffectResult?>> EffectResults = [];

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeAllEvent<DestinyDiceEffectExecutionEvent>(OnExecuteEffect);
        SubscribeAllEvent<DestinyDiceEffectGroupFinishEvent>(OnGroupFinish);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    private void OnExecuteEffect(DestinyDiceEffectExecutionEvent ev)
    {
        var uid = GetEntity(ev.Uid);
        var roller = GetEntity(ev.Roller);
        var grid = GetEntity(ev.Grid);
        if (!TryComp<DestinyDiceComponent>(uid, out var comp)) return;
        if (!EffectResults.TryGetValue(ev.Uid, out var dict))
        {
            dict = new Dictionary<IDestinyDiceEffect, DestinyDiceEffectResult?>();
            EffectResults[ev.Uid] = dict;
        }
        if (ev.Effect.DependsOn is not null)
            if (ev.Effect.DependsOn.Any(depId => dict.Keys.Any(kvp => kvp.EffectID == depId && dict[kvp]?.Success == false)))
            {
                dict[ev.Effect] = new DestinyDiceEffectResult(false);
                if (ev.Effect.FailureMessage != null)
                    _popup.PopupCoordinates(ev.Effect.FailureMessage, Transform(uid).Coordinates);
                return;
            }

        if (ev.Effect.Conditions is not null)
            foreach (var condition in ev.Effect.Conditions.Where(condition=>condition.RequiredToExecute))
            {
                if (TargetedExecution((uid, comp), condition, roller, (targets) =>
                    {
                        var passed = targets.Any(target =>
                        {
                            var result = CheckTriggerCondition(condition, target, (uid, comp), roller, grid);
                            return condition.FlipCondition ? !result : result;
                        });
                        Log.Log(LogLevel.Info, $"Checked condition {condition} where required was {condition.RequiredToExecute}, and returning with result {passed}");
                        return passed;
                    })) continue;
                dict[ev.Effect] = new DestinyDiceEffectResult(false);
                if (ev.Effect.FailureMessage != null)
                    _popup.PopupCoordinates(ev.Effect.FailureMessage, Transform(uid).Coordinates);
                return;
            }
        var result = TargetedExecution((uid, comp), ev.Effect, roller, (targets) =>
        {
            var passed = false;
            foreach (var target in targets)
            {
                var failedCondition = false;
                if (ev.Effect.Conditions is not null)
                    foreach (var condition in ev.Effect.Conditions.Where(condition=>!condition.RequiredToExecute))
                    {
                        var res = CheckTriggerCondition(condition, target, (uid, comp), roller, grid);
                        if (condition.FlipCondition) res = !res;
                        if (!res) failedCondition = true;
                    }
                if (failedCondition) continue;
                
                if (ExecuteEffect(ev.Effect, ev.SourceGroup, target, (uid, comp), roller, grid))
                    passed = true;
            }

            return passed;
        });
        Log.Log(LogLevel.Info, $"{result}");
        if (result)
        {
            if (ev.Effect.SuccessMessage != null)
                _popup.PopupCoordinates(ev.Effect.SuccessMessage, Transform(uid).Coordinates);
        }
        else
        {
            if (ev.Effect.FailureMessage != null)
                _popup.PopupCoordinates(ev.Effect.FailureMessage, Transform(uid).Coordinates);
        }

        dict[ev.Effect] = new DestinyDiceEffectResult(result);
    }

    // Yes, this sucks, unfortunately I am too stupid to figure out how to do this more efficiently than a switch statement
    // At least, efficient in a way that it is less tedious than just adding to this.
    protected bool CheckTriggerCondition(IDestinyDiceTriggerCondition condition, EntityUid target, Entity<DestinyDiceComponent> entity, EntityUid roller,
        EntityUid? grid)
    {
        switch (condition)
        {
            case ClothMuncherCondition clothMuncherCondition:
                {
                    if (!TryComp<BodyComponent>(target, out var body)) return false;
                    foreach (var part in body.RootContainer.ContainedEntities)
                    {
                        if (!TryComp<BodyPartComponent>(part, out var comp)) continue;
                        if(comp.PartType != BodyPartType.Torso) continue;
                        foreach (var organ in _container.GetAllContainers(part).SelectMany(container => container.ContainedEntities))
                        {
                            if (!TryComp<StomachComponent>(organ, out var stomach)) continue;
                            if (!stomach.IsSpecialDigestibleExclusive) continue;
                            if (stomach.SpecialDigestible?.Tags is null) continue;
                            if (!stomach.SpecialDigestible.Tags.Contains(_clothTag)) continue;
                            return true;
                        }
                    }
                    return false;
                }
            case DamageableCondition damageableCondition:
                {
                    return HasComp<DamageableComponent>(target);
                }
            case DamageGroupOverValueCondition damageGroupOverValueCondition:
                {
                    if (!TryComp<DamageableComponent>(target, out var comp)) return false;
                    if (!_proto.HasIndex<DamageGroupPrototype>(damageGroupOverValueCondition.Group)) return false;
                    var proto = _proto.Index<DamageGroupPrototype>(damageGroupOverValueCondition.Group);
                    if (!comp.Damage.TryGetDamageInGroup(proto, out var damage))
                        return false;
                    return !(damage.Value < damageGroupOverValueCondition.TargetValue);
               }
            case DamageTypeOverValueCondition damageTypeOverValueCondition:
               {
                   if (!TryComp<DamageableComponent>(target, out var comp)) return false;
                   if (!_proto.HasIndex<DamageTypePrototype>(damageTypeOverValueCondition.Type)) return false;
                   if (!comp.Damage.DamageDict.TryGetValue(damageTypeOverValueCondition.Type, out var damage))
                       return false;
                   return !(damage.Value < damageTypeOverValueCondition.TargetValue);
               }
            case TotalDamageOverValueCondition totalDamageOverValueCondition:
               {
                   if (!TryComp<DamageableComponent>(target, out var comp)) return false;
                   return !(comp.Damage.GetTotal() < totalDamageOverValueCondition.TargetValue);
               }
            case IsMobStateCondition isMobStateCondition:
                {
                    if(!TryComp<MobStateComponent>(target, out var comp)) return false;
                    return comp.CurrentState == isMobStateCondition.TargetState;
                }
            case IsNotBeingContainedCondition isNotBeingContainedCondition:
                {
                    var containers = _container.GetContainingContainers(target);
                    return !containers.Any();
                }
            case IsNotBeingHeldCondition isNotBeingHeldCondition:
                {
                    var containers = _container.GetContainingContainers(target);
                    foreach (var container in containers)
                    {
                        if (!TryComp<HandsComponent>(container.Owner, out var hands)) continue;
                        if (hands.Hands.Any(hand => container.ID == hand.Key)) return false;
                    }
                    return true;
                }
            default:
                return false;
        }
    }
    
    private void OnGroupFinish(DestinyDiceEffectGroupFinishEvent ev) => EffectResults.Remove(ev.Uid);

    protected virtual void OnRestart(RoundRestartCleanupEvent ev) => EffectResults.Clear();

    protected virtual bool ExecuteEffect(IDestinyDiceEffect effect, DestinyDiceEffectGroup group, EntityUid target, Entity<DestinyDiceComponent> entity,
        EntityUid roller, EntityUid? grid) => true;
    
    protected TransformComponent GetCorrectTransform(EntityUid target, Entity<DestinyDiceComponent> die, EntityUid roller)
    {
        if (target != die.Owner || !TryComp<HandsComponent>(roller, out var hands)) return Transform(target);
        var containerEnumerator = _container.GetContainingContainers(target);
        var baseContainers = containerEnumerator.ToList();
        if(baseContainers.Count != 0) return Transform(baseContainers.Last().Owner);
        return hands.Hands.Keys
            .Select(hand => _hands.GetHeldItem((roller, hands), hand))
            .Any(item => item == target) ? Transform(roller) : Transform(target);
    }
    
    protected List<EntityUid> GetPrototypesOnMap(IDestinyDiceTargetable effect, EntityPrototype proto, MapId mapId)
    {
        var entities = EntityManager.GetEntities().Where(ent =>
            MetaData(ent).EntityPrototype == proto && Transform(ent).MapID == mapId);

        if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
        return entities.ToList();
    }

    protected List<EntityUid> GetPrototypesNearby(IDestinyDiceTargetable effect, EntityPrototype proto, EntityUid sourceEntity, float range)
    {
        var entities = _lookup.GetEntitiesInRange(sourceEntity, range)
            .Where(ent => MetaData(ent).EntityPrototype == proto);
        if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
        return entities.ToList();
    }

    protected List<EntityUid> GetAllPrototypes(IDestinyDiceTargetable effect, EntityPrototype proto)
    {
        var entities = EntityManager.GetEntities().Where(ent => MetaData(ent).EntityPrototype == proto);
        if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
        return entities.ToList();
    }
    
    protected List<EntityUid> GetPlayersOnMap(IDestinyDiceTargetable effect, MapId mapId)
    {
        var entities = new HashSet<Entity<ActorComponent>>();
        _lookup.GetEntitiesOnMap(mapId, entities);

        if (!effect.AllowGhosts) entities.RemoveWhere(ent => HasComp<GhostComponent>(ent));
        return entities.Select(e => e.Owner).ToList();
    }

    protected List<EntityUid> GetPlayersNearby(IDestinyDiceTargetable effect, EntityUid sourceEntity, float range)
    {
        var entities = _lookup.GetEntitiesInRange(sourceEntity,
            range).Where(HasComp<ActorComponent>);
        if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
        return entities.ToList();
    }

    protected List<EntityUid> GetAllPlayers(IDestinyDiceTargetable effect)
    {
        var entities = EntityManager.GetEntities().Where(HasComp<ActorComponent>);
        if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
        return entities.ToList();
    }

    protected bool TargetedExecution(Entity<DestinyDiceComponent> entity, IDestinyDiceTargetable effect, EntityUid roller, Func<List<EntityUid>, bool> callback)
    {
        switch (effect)
        {
            case { TargetEntity: false, TargetPlayer: false }:
                {
                    var ent = new List<EntityUid> { entity };
                    return callback(ent);
                }
            case { TargetPlayer: true, TargetMultiple: false }:
                {
                    var ent = new List<EntityUid> { roller };
                    return callback(ent);
                }
            case { TargetPlayer: true, TargetMultiple: true }:
                {
                    if (effect.Range < 0)
                    {
                        var entities = GetAllPlayers(effect);
                        return callback(entities);
                    }
                    if (float.IsPositiveInfinity(effect.Range))
                    {
                        var entities = GetPlayersOnMap(effect, Transform(entity).MapID);
                        return callback(entities);
                    }
                    else
                    {
                        var entities = GetPlayersNearby(effect, entity, effect.Range);
                        return callback(entities);
                    }
                }
        }

        if (!effect.TargetEntity) return false;
        if (!_proto.TryIndex(effect.TargetProto, out var proto)) return false;
        if (effect.Range < 0)
        {
            var entities = GetAllPrototypes(effect, proto);
            return callback(entities);
        }
        if (float.IsPositiveInfinity(effect.Range))
        {
            var entities = GetPrototypesOnMap(effect, proto, Transform(entity).MapID);
            return callback(entities);
        }
        else
        {
            var entities = GetPrototypesNearby(effect, proto, entity, effect.Range);
            return callback(entities);
        }
    }
}