using System.Linq;
using Content.Shared._Starlight.Evolving;
using Content.Shared._Starlight.Weapons.Melee.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared._Starlight.Evolving.Conditions;
using Content.Shared.Actions;
using Content.Shared._Starlight.Antags.TerrorSpider;
using Content.Shared._Starlight.Spider.Events;
using Content.Shared.Objectives.Systems;
using Content.Shared.Mind.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Evolving.EntitySystems;

public abstract class SharedEvolvingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectivesSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvolvingComponent, EvolveEvent>(OnEvolve);
        SubscribeLocalEvent<EvolvingComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<EvolvingComponent, MindRemovedMessage>(OnMindRemoved);

        // Watchers

        SubscribeLocalEvent<EvolvingComponent, AfterMeleeHitEvent>(AfterMeleeHit); // Damage Deal Condition
        SubscribeLocalEvent<EvolvingComponent, EggsInjectedEvent>(OnEggsInjected); // Eggs Inject Condition
        SubscribeLocalEvent<EvolvingComponent, SpiderWebSpawnedEvent>(OnSpiderWebSpawn);
    }

    // TODO: Make all of this shit generalized, so you don't just copy and paste.
    #region Watchers
    private void AfterMeleeHit(EntityUid uid, EvolvingComponent component, AfterMeleeHitEvent args)
    {
        if (!_timing.IsFirstTimePredicted || args.Handled || args.HitEntities.Count <= 0)
            return;

        foreach (var condition in component.Conditions)
        {
            if (condition is DamageDealCondition damageDealCondition)
            {
                if (damageDealCondition.Condition()) // If condition already met, we don't need to update it and do unnecessary linq operations
                    continue;
                if (!damageDealCondition.OnlyAlive
                    || args.HitEntities.All(entity => _mobStateSystem.IsAlive(entity))) // Melee hit event don't have separated damage amount, so...
                    damageDealCondition.AddDamage(args.DealedDamage.GetTotal().Float()); // Just update the damage dealt if all alive/we don't need only alive.
            }
        }
        TryUpdateEvolveState(uid, component, EvolveType.DamageDeal); // So when we update damage, we can try to add action if we can.
    }

    private void OnEggsInjected(EntityUid uid, EvolvingComponent component, EggsInjectedEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        foreach (var condition in component.Conditions)
        {
            if (condition is EggsInjectCondition eggsInject)
            {
                if (eggsInject.Condition())
                    continue;
                eggsInject.UpdateEggs(1);
            }
        }

        TryUpdateEvolveState(uid, component, EvolveType.EggsInjected);
    }

    private void OnSpiderWebSpawn(EntityUid uid, EvolvingComponent component, SpiderWebSpawnedEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        foreach (var condition in component.Conditions)
        {
            if (condition is SpiderWebCondition spiderWeb)
            {
                if (spiderWeb.Condition())
                    continue;
                spiderWeb.UpdateWebs(1);
            }
        }

        TryUpdateEvolveState(uid, component, EvolveType.SpiderWebsSpawned);
    }

    #endregion

    #region Logic

    private void OnMindAdded(EntityUid uid, EvolvingComponent component, MindAddedMessage args)
    {
        if (component.Objectives.Count > 0)
            foreach (var obj in component.Objectives)
                _mindSystem.AddObjective(args.Mind.Owner, args.Mind.Comp, obj);
        else
            TryUpdateObjective(uid, component, null, false); // Just add starting objectives.
    }

    private void OnMindRemoved(EntityUid uid, EvolvingComponent component, MindRemovedMessage args) => TryRemoveObjectives(args.Mind.Owner, args.Mind.Comp, component, delete: false, force: true);

    private bool TryUpdateEvolveState(EntityUid uid, EvolvingComponent component, EvolveType? objType = null)
    {
        TryAddAction(uid, component);

        if (objType != null)
            return TryUpdateObjective(uid, component, objType, true);
        return false;
    }

    private bool TryUpdateObjective(EntityUid uid, EvolvingComponent component, EvolveType? objType, bool increment)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return false;

        List<EntityUid> objectivesToUpdate = new();
        
        foreach (var obj in mind.Objectives)
        {
            if (!HasComp<EvolveConditionComponent>(obj))
                continue;
            
            objectivesToUpdate.Add(obj);
        }

        if (objectivesToUpdate.Count == 0)
        {
            foreach (var condition in component.Conditions)
            {
                var objEnt = TryInitObjectives(mindId, mind, component.ObjectiveId, condition);

                _mindSystem.AddObjective(mindId, mind, objEnt);
                component.Objectives.Add(objEnt);
            }
        }
        else if (increment)
            foreach (var objective in objectivesToUpdate)
                if (TryComp<EvolveConditionComponent>(objective, out var evolveCondition) 
                    && (objType == null || evolveCondition.ConditionType == objType))
                    evolveCondition.Count += 1;
        
        return true;
    }

    public virtual EntityUid TryInitObjectives(EntityUid mindId, MindComponent mind, string objectiveId, EvolvingCondition condition)
    {
        var obj = _objectivesSystem.TryCreateObjective(mindId, mind, objectiveId);
        if (obj is not { Valid: true } objEnt)
            return EntityUid.Invalid;

        if (TryComp<EvolveConditionComponent>(objEnt, out var evolveCondition))
            evolveCondition.ConditionType = condition.Type;

        return objEnt;
    }

    /// <summary>
    /// Tries to add evolve action to the entity if it can.
    /// </summary>
    /// <param name="uid">Target Entity</param>
    /// <param name="component">Evolving Component</param>
    /// <returns>Whether or not the action added</returns>
    private bool TryAddAction(EntityUid uid, EvolvingComponent component)
    {
        if (component.EvolveActionEntity != null // If EvolveActionEntity not null, we already added action.
            || !CanEvolve(uid, component))
            return false;

        component.EvolveActionEntity = _actionsSystem.AddAction(uid, component.EvolveActionId);

        return component.EvolveActionEntity != null;
    }

    /// <summary>
    ///   Tries to evolve the entity if it can.
    /// </summary>
    /// <param name="uid">Entity which evolve to...</param>
    /// <param name="component">Evolving Component</param>
    /// <returns>Whether or not the entity evolved</returns>
    private bool TryEvolve(EntityUid uid, EvolvingComponent component)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind)
            || !CanEvolve(uid, component))
            return false;

        // All conditions met, evolve.
        foreach (var obj in component.Objectives)
            _mindSystem.TryRemoveObjective(mindId, mind, obj, force: true); // Clear out objectives.

        var ent = EntityManager.PredictedSpawnAtPosition(component.EvolveTo, Transform(uid).Coordinates);
        _mindSystem.TransferTo(mindId, ent, mind: mind);
        QueueDel(uid);
        return true;
    }

    private bool TryRemoveObjectives(EntityUid uid, EvolvingComponent component, bool delete = true, bool force = false)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return false;
        
        return TryRemoveObjectives(mindId, mind, component, delete, force);
    }

    private bool TryRemoveObjectives(EntityUid mindId, MindComponent mind, EvolvingComponent component, bool delete = true, bool force = false)
    {        
        bool removedAny = false;
        foreach (var obj in component.Objectives)
            if (_mindSystem.TryRemoveObjective(mindId, mind, obj, delete: delete, force: force))
                removedAny = true;

        return removedAny;
    }

    private void OnEvolve(EntityUid uid, EvolvingComponent component, EvolveEvent args) => TryEvolve(uid, component);

    /// <summary>
    ///  Checks if the entity can evolve.
    /// </summary>
    /// <param name="uid">Target Entity</param>
    /// <param name="component">Evolving Component</param>
    /// <returns>Whether or not the entity can evolve</returns>
    private bool CanEvolve(EntityUid uid, EvolvingComponent component) => component.Conditions.All(c => c.Condition(new EvolvingConditionArgs(uid, component.EvolveActionEntity, EntityManager)));
    #endregion
}