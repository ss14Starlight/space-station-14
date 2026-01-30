using Content.Shared.Mind;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionClearObjectives : OnSignAction
{

    private IEntityManager _entityManager = default!;
    private SharedMindSystem _mind = default!;

    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        if (!_entityManager.TryGetComponent<ActorComponent>(target, out var actor))
            return false; //oh well they dont have a actor. let the other stuff run.
        if (!_mind.TryGetMind(actor.PlayerSession.UserId, out var mindId, out var mind))
            return false; //oh well they dont have a mind. let the other actions run.
        while (_mind.TryRemoveObjective(mindId.Value, mind, 0)) { }// basically just keep trying to remove the 0th objective until there is none
        return false;
    }

    public override void ResolveIoC()
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _mind = _entityManager.System<SharedMindSystem>();
    }
}
