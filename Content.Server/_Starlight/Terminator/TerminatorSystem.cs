using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Terminator;

public sealed partial class TerminatorSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly GameTicker _game = default!;

    private EntProtoId SpawnRulePrototype = "TerminatorSpawn";

    public bool CreateTerminator(EntityUid target)
    {
        var uid = _game.AddGameRule(SpawnRulePrototype);
        var comp = EnsureComp<TerminatorRuleComponent>(uid);

        if (!_mind.TryGetMind(target, out var mindId, out var mind)) return false;
        comp.Target = mindId;
        comp.TargetBody = target;
        _game.StartGameRule(uid);
        return true;
    }
}