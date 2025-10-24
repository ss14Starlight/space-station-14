using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionAddAntag : OnSignAction
{
    /// <summary>
    /// what antags should be added to the target.
    /// </summary>
    [DataField]
    public List<AntagCompPair> Antags = [];

    /// <summary>
    /// whether to summon a paradox clone of the target
    /// </summary>
    [DataField]
    public bool ParadoxClone = false;

    private AntagSelectionSystem _antag = default!;
    private GameTicker _gameTicker = default!;
    private IComponentFactory _componentFactory = default!;
    private IEntityManager _entityManager = default!;

    private readonly EntProtoId _paradoxCloneRuleId = "ParadoxCloneSpawn";

    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        if (!_entityManager.TryGetComponent(target, out ActorComponent? actor))
            return false;

        foreach (var antag in Antags)
        {
            var targetComp = _componentFactory.GetComponent(antag.TargetComponent);

            var fmakeantag = typeof(AntagSelectionSystem).GetMethod(nameof(AntagSelectionSystem.ForceMakeAntag));
            if (fmakeantag == null)
            {
                continue;
            }
            var generic = fmakeantag.MakeGenericMethod(targetComp.GetType());
            generic.Invoke(_antag, [actor.PlayerSession, antag.Antag.Id]);
        }

        if (!ParadoxClone) return false;
        var ruleEnt = _gameTicker.AddGameRule(_paradoxCloneRuleId);

        if (!_entityManager.TryGetComponent<ParadoxCloneRuleComponent>(ruleEnt, out var paradoxCloneRuleComp))
            return false;

        paradoxCloneRuleComp.OriginalBody = target; // override the target player

        _gameTicker.StartGameRule(ruleEnt);

        return false;
    }

    public override void ResolveIoC()
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _antag = _entityManager.System<AntagSelectionSystem>();
        _gameTicker = _entityManager.System<GameTicker>();
        _componentFactory = IoCManager.Resolve<IComponentFactory>();
    }
}