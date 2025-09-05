
//Thats a mouthful
using Content.Server.CombatMode;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators.Interactions;
using Content.Shared._Starlight.Language;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators.Interactions;

public sealed partial class UseActionOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private SharedDoAfterSystem _doAfterSystem = default!;
    private SharedActionsSystem _actions = default!;

    [DataField]
    public string TargetKey = "TargetTile";

    /// <summary>
    /// Exit with failure if doafter wasn't raised
    /// </summary>
    [DataField]
    public bool ExpectDoAfter = false;


    [DataField(required: true)]
    public EntProtoId<ActionComponent> ActionID = "";

    //WHY ISN'T THIS STATIC IN InteractWithOperator
    public string CurrentDoAfter = "CurrentInteractWithDoAfter";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _doAfterSystem = sysManager.GetEntitySystem<SharedDoAfterSystem>();
        _actions = sysManager.GetEntitySystem<SharedActionsSystem>();
    }

    // Ensure that CurrentDoAfter doesn't exist as we enter this operator,
    // the code currently relies on the result of a TryGetValue
    public override void Startup(NPCBlackboard blackboard)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<ActionsComponent>(owner, out var actionsComponent))
            return HTNOperatorStatus.Failed;

        // Handle ongoing doAfter, and store the doAfter.nextId so we can detect if we started one
        ushort nextId = 0;
        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out var doAfter))
        {
            // if CurrentDoAfter contains something, we have an active doAfter
            if (blackboard.TryGetValue<ushort>(CurrentDoAfter, out var doAfterId, _entManager))
            {
                var status = _doAfterSystem.GetStatus(owner, doAfterId, null);
                return status switch
                {
                    DoAfterStatus.Running => HTNOperatorStatus.Continuing,
                    DoAfterStatus.Finished => HTNOperatorStatus.Finished,
                    _ => HTNOperatorStatus.Failed
                };
            }

            nextId = doAfter.NextId;
        }

        if (blackboard.TryGetValue<object>(TargetKey, out var target, _entManager))
        {
            BaseActionEvent? ev = null;
            Entity<ActionComponent>? actionToUse = null;
            foreach (var action in _actions.GetActions(owner))
            {
                if (!_entManager.TryGetComponent<MetaDataComponent>(action, out var meta))
                    continue; //FYM you dont have a MetaDataComponent. it comes with your ECS.
                var proto = meta.EntityPrototype;
                if (proto == null)
                    continue; //it doesn't have a prototype we can check against
                if (proto.ID != ActionID)
                    continue; //Not the action we wanna use in this case.

                if (_entManager.TryGetComponent<EntityTargetActionComponent>(action, out var entTargetAction) && target is EntityUid entityTarget)
                {
                    var act = entTargetAction.Event;
                    if (act == null)
                        continue;
                    act.Target = entityTarget;
                    ev = act;
                    actionToUse = action;
                    break;
                }
                if (_entManager.TryGetComponent<WorldTargetActionComponent>(action, out var worldTargetAction) && target is EntityCoordinates targetCoords)
                {
                    var act = worldTargetAction.Event;
                    if (act == null)
                        continue;
                    act.Target = targetCoords;
                    ev = act;
                    actionToUse = action;
                    break;
                }

            }
            if (actionToUse == null)
                return HTNOperatorStatus.Failed;
            _actions.PerformAction((owner, actionsComponent), actionToUse.Value, ev);
        }


        // Detect doAfter, save it, and don't exit from this operator
        if (doAfter != null && nextId != doAfter.NextId)
        {
            blackboard.SetValue(CurrentDoAfter, nextId);
            return HTNOperatorStatus.Continuing;
        }

        // We shouldn't arrive here if we start a doafter, so fail if we expected a doafter
        if (ExpectDoAfter)
            return HTNOperatorStatus.Failed;

        return HTNOperatorStatus.Finished;
    }
}
