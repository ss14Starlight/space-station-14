using System.Linq;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Dice.DestinyDice;

[Prototype]
public sealed class DestinyDiceEffectPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    public required IDestinyDiceEffect Effect { get; set; }

    public List<IDestinyDiceTriggerCondition> Conditions { get; set; } = [];

    public List<IDestinyDiceTarget> Targets { get; set; } = [];
}

    // protected TransformComponent GetCorrectTransform(EntityUid target, Entity<DestinyDiceComponent> die, EntityUid roller)
    // {
    //     if (target != die.Owner || !TryComp<HandsComponent>(roller, out var hands)) return Transform(target);
    //     var containerEnumerator = _container.GetContainingContainers(target);
    //     var baseContainers = containerEnumerator.ToList();
    //     if(baseContainers.Count != 0) return Transform(baseContainers.Last().Owner);
    //     return hands.Hands.Keys
    //         .Select(hand => _hands.GetHeldItem((roller, hands), hand))
    //         .Any(item => item == target) ? Transform(roller) : Transform(target);
    // }
    //
    // protected List<EntityUid> GetPrototypesOnMap(IDestinyDiceTargetable effect, EntityPrototype proto, MapId mapId)
    // {
    //     var entities = EntityManager.GetEntities().Where(ent =>
    //         MetaData(ent).EntityPrototype == proto && Transform(ent).MapID == mapId);
    //
    //     if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
    //     return entities.ToList();
    // }
    //
    // protected List<EntityUid> GetPrototypesNearby(IDestinyDiceTargetable effect, EntityPrototype proto, EntityUid sourceEntity, float range)
    // {
    //     var entities = _lookup.GetEntitiesInRange(sourceEntity, range)
    //         .Where(ent => MetaData(ent).EntityPrototype == proto);
    //     if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
    //     return entities.ToList();
    // }
    //
    // protected List<EntityUid> GetAllPrototypes(IDestinyDiceTargetable effect, EntityPrototype proto)
    // {
    //     var entities = EntityManager.GetEntities().Where(ent => MetaData(ent).EntityPrototype == proto);
    //     if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
    //     return entities.ToList();
    // }
    //
    // protected List<EntityUid> GetPlayersOnMap(IDestinyDiceTargetable effect, MapId mapId)
    // {
    //     var entities = new HashSet<Entity<ActorComponent>>();
    //     _lookup.GetEntitiesOnMap(mapId, entities);
    //
    //     if (!effect.AllowGhosts) entities.RemoveWhere(ent => HasComp<GhostComponent>(ent));
    //     return entities.Select(e => e.Owner).ToList();
    // }
    //
    // protected List<EntityUid> GetPlayersNearby(IDestinyDiceTargetable effect, EntityUid sourceEntity, float range)
    // {
    //     var entities = _lookup.GetEntitiesInRange(sourceEntity,
    //         range).Where(HasComp<ActorComponent>);
    //     if (!effect.AllowGhosts) entities = entities.Where(ent => !HasComp<GhostComponent>(ent));
    //     return entities.ToList();
    // }

public interface IDestinyDiceTarget
{
    public float? Range { get; set; }
    public bool AllowGhosts { get; set; }
    public List<EntityUid>? Resolve(DestinyDiceEffectExecutionEvent ev);
}

[DataRecord]
public sealed class DieTarget : IDestinyDiceTarget
{
    [Dependency] private readonly IEntityManager _em = default!;
    
    public float? Range { get; set; } // Ignored
    public bool AllowGhosts { get; set; }

    public List<EntityUid>? Resolve(DestinyDiceEffectExecutionEvent ev)
    {
        List<EntityUid> target = [_em.GetEntity(ev.Uid)];
        return target;
    }
}

[DataRecord]
public sealed class RollerTarget : IDestinyDiceTarget
{
    [Dependency] private readonly IEntityManager _em = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    
    public float? Range { get; set; }
    public bool AllowGhosts { get; set; }

    public List<EntityUid>? Resolve(DestinyDiceEffectExecutionEvent ev)
    {
        var die = _em.GetEntity(ev.Uid);
        var roller = _em.GetEntity(ev.Roller);
        List<EntityUid>? target = [];
        if (Range is null || Range < 0 || float.IsPositiveInfinity(Range.Value) ||
            _xform.InRange(die, roller, Range.Value)) target.Add(roller);
        if (target.Count == 0) return null;
        if (_em.HasComponent<GhostComponent>(target.First()) && !AllowGhosts) return null;
        return target;
    }
}

[DataRecord]
public sealed class NearbyPlayersTarget : IDestinyDiceTarget
{
    [Dependency] private readonly IEntityManager _em = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    
    public float? Range { get; set; }
    public bool AllowGhosts { get; set; }

    public List<EntityUid>? Resolve(DestinyDiceEffectExecutionEvent ev)
    {
        var die = _em.GetEntity(ev.Uid);
        List<EntityUid>? targets;
        var range = Range ?? 1;
        switch (range)
        {
            case < 0:
                targets = _em.GetEntities().Where(ent => _em.HasComponent<ActorComponent>(ent)).ToList();
                break;
            default:
                {
                    if (float.IsPositiveInfinity(range))
                        targets = _em.GetEntities().Where(ent =>
                            _em.HasComponent<ActorComponent>(ent) && _em.GetComponent<TransformComponent>(ent).MapID ==
                            _em.GetComponent<TransformComponent>(die).MapID).ToList();
                    else
                        targets = _lookup.GetEntitiesInRange(die, range)
                            .Where(ent => _em.HasComponent<ActorComponent>(ent)).ToList();
                    break;
                }
        }

        if (!AllowGhosts) targets = targets.Where(ent => !_em.HasComponent<GhostComponent>(ent)).ToList();
        return targets.Count == 0 ? null : targets;
    }
}

[DataRecord]
public sealed class EntityTarget : IDestinyDiceTarget
{
    [Dependency] private readonly IEntityManager _em = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    
    public float? Range { get; set; }
    public bool AllowGhosts { get; set; }

    public List<EntityUid>? Resolve(DestinyDiceEffectExecutionEvent ev)
    {
        var die = _em.GetEntity(ev.Uid);
        List<EntityUid>? targets;
        var range = Range ?? 1;
        switch (range)
        {
            case < 0:
                targets = _em.GetEntities().Where(ent => _em.HasComponent<ActorComponent>(ent)).ToList();
                break;
            default:
                {
                    if (float.IsPositiveInfinity(range))
                        targets = _em.GetEntities().Where(ent =>
                            _em.HasComponent<ActorComponent>(ent) && _em.GetComponent<TransformComponent>(ent).MapID ==
                            _em.GetComponent<TransformComponent>(die).MapID).ToList();
                    else
                        targets = _lookup.GetEntitiesInRange(die, range)
                            .Where(ent => _em.HasComponent<ActorComponent>(ent)).ToList();
                    break;
                }
        }

        if (!AllowGhosts) targets = targets.Where(ent => !_em.HasComponent<GhostComponent>(ent)).ToList();
        return targets.Count == 0 ? null : targets;
    }
}