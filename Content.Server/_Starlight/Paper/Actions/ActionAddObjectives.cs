using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Objectives;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionAddObjectives : OnSignAction
{

    private IEntityManager _entityManager = default!;
    private SharedMindSystem _mind = default!;
    private ObjectivesSystem _objectives = default!;
    private IRobustRandom _random = default!;

    /// <summary>
    /// what objectives should be added to the person.
    /// </summary>
    [DataField]
    public List<EntProtoId<ObjectiveComponent>> Objectives = [];

    /// <summary>
    /// the list of objective groups to add to the antag
    /// </summary>
    [DataField]
    public List<AntagObjectiveSet> Sets = [];

    /// <summary>
    /// What is the max difficulty that can be assigned from the sets defaults to "basically infinite"
    /// </summary>
    [DataField]
    public float maxDifficulty = 999999;

    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        if (!_entityManager.TryGetComponent<ActorComponent>(target, out var actor))
            return false; //oh well they dont have a actor. let the other stuff run.
        if (!_mind.TryGetMind(actor.PlayerSession.UserId, out var mindId, out var mind))
            return false; //oh well they dont have a mind. let the other actions run.

        foreach (var objective in Objectives)
            _mind.TryAddObjective(mindId.Value, mind, objective.Id);

        var remainingDifficulty = maxDifficulty;
        foreach (var set in Sets)
        {
            if (!_random.Prob(set.Prob))
                continue;
            if (_objectives.GetRandomObjective(mindId.Value, mind, set.Groups, remainingDifficulty) is { } obj)
            {
                _mind.AddObjective(mindId.Value, mind, obj);
                remainingDifficulty -= _entityManager.GetComponent<ObjectiveComponent>(obj).Difficulty;
            }
        }

        return false;
    }

    public override void ResolveIoC()
    {
        _random = IoCManager.Resolve<IRobustRandom>();
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _mind = _entityManager.System<SharedMindSystem>();
        _objectives = _entityManager.System<ObjectivesSystem>();
    }
}
