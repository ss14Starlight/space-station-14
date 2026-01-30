using Content.Server.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionAddGamerules : OnSignAction
{
    /// <summary>
    /// What game rules are added.
    /// </summary>
    [DataField]
    public List<EntProtoId<GameRuleComponent>> Rules = [];

    private GameTicker _gameTicker = default!;

    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        foreach (var rule in Rules)
        {
            var ent = _gameTicker.AddGameRule(rule.Id);
            _gameTicker.StartGameRule(ent);
        }

        return false;
    }

    public override void ResolveIoC()
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        _gameTicker = entMan.System<GameTicker>();
    }
}