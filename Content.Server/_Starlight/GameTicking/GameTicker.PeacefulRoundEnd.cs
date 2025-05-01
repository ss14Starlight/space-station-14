using Content.Server.GameTicking;
using Content.Shared.Starlight.CCVar;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Movement.Components;
using Content.Shared.Mech.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;

namespace Content.Server.Starlight.GameTicking;

public sealed class PeacefulRoundEndSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    private bool _isEnabled = false;
    private bool _roundedEnded = false;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(StarlightCCVars.PeacefulRoundEnd, v => _isEnabled = v, true);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnded);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }
    
    private void SpreadPeace()
    {
        if (!_isEnabled || !_roundedEnded) return;
        foreach (var mob in EntityQuery<MobMoverComponent>())
        {
            EnsureComp<PacifiedComponent>(mob.Owner);
        }
        foreach (var mob in EntityQuery<MechComponent>())
        {
            EnsureComp<PacifiedComponent>(mob.Owner);
        }
    }
    
    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        SpreadPeace();
    }

    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        _roundedEnded = true;
        SpreadPeace();
    }
}
