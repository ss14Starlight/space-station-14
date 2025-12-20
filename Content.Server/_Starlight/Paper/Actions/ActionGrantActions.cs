using Content.Server.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionGrantAction : OnSignAction
{
    /// <summary>
    /// what actions should be granted to the target
    /// </summary>
    [DataField]
    public List<EntProtoId<ActionComponent>> Actions = [];
    
    private IEntityManager _entityManager = default!;
    private ActionsSystem _actionsSystem = default!;
    
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        foreach (var action in Actions)
        {
            EntityUid? actionEnt = null;
            _actionsSystem.AddAction(target, ref actionEnt, action);
        }
        return false;
    }

    public override void ResolveIoC()
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _actionsSystem = _entityManager.System<ActionsSystem>();
    }
}