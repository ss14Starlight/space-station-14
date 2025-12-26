using Content.Server.Actions;
using Content.Shared._Starlight.Devil.DamnationActions;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionAddAction : DamnationAction
{
    [DataField]
    List<ProtoId<EntityPrototype>> Actions = new();
    private ActionsSystem _actions = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        foreach (var action in Actions)
        {
            _actions.AddAction(victim, action);
        }

        return true;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();
        
        _actions = _entityManager.System<ActionsSystem>();
    }
}