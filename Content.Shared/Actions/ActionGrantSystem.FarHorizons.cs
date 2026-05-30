using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.Actions;

public sealed partial class ActionGrantSystem
{

    public void AddAction(Entity<ActionGrantComponent> ent, EntProtoId action)
    {
        List<EntProtoId> actions = [];
        actions.Add(action);
        AddActions(ent, actions);
    }

    public void AddActions(Entity<ActionGrantComponent> ent, List<EntProtoId> actions)
    {
        var newActions = ent.Comp.Actions.Union(actions).ToList();
        if (newActions == ent.Comp.Actions)
            return;

        ActionGrantComponent combinedComp = new()
        {
            Actions = newActions
        };

        EntityManager.RemoveComponent<ActionGrantComponent>(ent);
        EntityManager.AddComponent(ent, combinedComp);
    }

    public void RemoveAction(Entity<ActionGrantComponent> ent, EntProtoId action)
    {
        List<EntProtoId> actions = [];
        actions.Add(action);
        RemoveActions(ent, actions);
    }

    public void RemoveActions(Entity<ActionGrantComponent> ent, List<EntProtoId> actions)
    {
        var newActions = ent.Comp.Actions.Except(actions).ToList();
        if (newActions == ent.Comp.Actions)
            return;

        ActionGrantComponent decomposedComp = new()
        {
            Actions = newActions
        };

        EntityManager.RemoveComponent<ActionGrantComponent>(ent);
        EntityManager.AddComponent(ent, decomposedComp);
    }
}
