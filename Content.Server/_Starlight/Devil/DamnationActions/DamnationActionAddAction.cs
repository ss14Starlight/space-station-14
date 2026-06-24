using Content.Shared._Starlight.Devil.DamnationActions;
using Content.Shared._Starlight.Devil;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionAddAction : SharedDamnationActionAddAction
{
    private Dictionary<EntityUid, List<EntityUid>> ProvidedActions = new();

    public override bool Action(Entity<DamnedComponent> victim)
    {
        // we need action uids on server, so outright replace shared method
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
}
