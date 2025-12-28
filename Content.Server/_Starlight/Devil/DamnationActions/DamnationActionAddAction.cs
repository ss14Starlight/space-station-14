using Content.Server.Actions;
using Content.Shared._Starlight.Devil.DamnationActions;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionAddAction : DamnationAction
{
    [DataField]
    List<ProtoId<EntityPrototype>> Actions = new();
    private ActionsSystem _actions = default!;

    private Dictionary<EntityUid, List<EntityUid>> ProvidedActions = new();

    public override bool Action(Entity<DamnedComponent> victim)
    {
        if(!ProvidedActions.ContainsKey(victim)) ProvidedActions[victim] = new();
        foreach (var action in Actions)
        {
            var uid = _actions.AddAction(victim, action);
            if(uid is EntityUid id) ProvidedActions[victim].Add(id);
        }

        return true;
    }

    public override bool ReverseAction(Entity<DamnedComponent> victim)
    {
        foreach (var actionId in ProvidedActions[victim])
        {
            _actions.RemoveAction(actionId);
        }
        ProvidedActions.Remove(victim);

        return true;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();
        
        _actions = _entityManager.System<ActionsSystem>();
    }
}