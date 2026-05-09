using Robust.Shared.Prototypes;
using Content.Shared.Actions;

namespace Content.Shared._Starlight.Devil.DamnationActions;

public abstract partial class SharedDamnationActionAddAction : DamnationAction
{
    [DataField]
    public List<ProtoId<EntityPrototype>> Actions = new();

    protected SharedActionsSystem _actions = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        foreach (var action in Actions) _actions.AddAction(victim, action);
        return true;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();

        _actions = _entityManager.System<SharedActionsSystem>();
    }
}
