using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Terminator;

public sealed partial class TerminatorSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private GameTicker _game = default!;

    private readonly EntProtoId _spawnRulePrototype = "TerminatorSpawn";

    public bool CreateTerminator(EntityUid target)
    {
        var uid = _game.AddGameRule(_spawnRulePrototype);
        var comp = EnsureComp<TerminatorRuleComponent>(uid);

        if (!_mind.TryGetMind(target, out var mindId, out var mind)) return false;
        comp.Target = mindId;
        comp.TargetBody = target;
        _game.StartGameRule(uid);
        return true;
    }
}
