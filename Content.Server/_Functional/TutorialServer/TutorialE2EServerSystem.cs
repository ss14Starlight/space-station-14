using Content.Server.GameTicking;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// E2E helper: after a player readies in lobby, force-start TutorialServer and ensure they join.
/// </summary>
public sealed partial class TutorialE2EServerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private GameTicker _ticker = default!;

    private TimeSpan? _forceStartAt;
    private TimeSpan? _joinPassAt;
    private bool _startedRound;

    public override void Initialize()
    {
        base.Initialize();
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (!_cfg.GetCVar(TutorialCVars.E2EForceStart))
            return;

        if (e.NewStatus != SessionStatus.InGame)
            return;

        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby)
            return;

        // Wait for client auto-ready, then start.
        _forceStartAt = _timing.CurTime + TimeSpan.FromSeconds(2);
        Log.Info("TUTORIAL_E2E: scheduling force_start (waiting for ready)");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_cfg.GetCVar(TutorialCVars.E2EForceStart) && _forceStartAt != null && _timing.CurTime >= _forceStartAt)
        {
            _forceStartAt = null;

            if (_ticker.RunLevel == GameRunLevel.PreRoundLobby)
            {
                var anyReady = false;
                foreach (var (_, status) in _ticker.PlayerGameStatuses)
                {
                    if (status == PlayerGameStatus.ReadyToPlay)
                    {
                        anyReady = true;
                        break;
                    }
                }

                if (!anyReady)
                {
                    // Client may still be loading lobby UI — retry shortly.
                    _forceStartAt = _timing.CurTime + TimeSpan.FromSeconds(2);
                    Log.Info("TUTORIAL_E2E: no ready players yet; deferring force_start");
                    return;
                }

                Log.Info("TUTORIAL_E2E: force_start round");
                _ticker.StartRound();
                _startedRound = true;
                _joinPassAt = _timing.CurTime + TimeSpan.FromSeconds(1);
            }
        }

        if (_startedRound && _joinPassAt != null && _timing.CurTime >= _joinPassAt &&
            _ticker.RunLevel == GameRunLevel.InRound)
        {
            _joinPassAt = null;
            EnsurePlayersJoined();
        }
    }

    private void EnsurePlayersJoined()
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity != null)
                continue;

            Log.Info($"TUTORIAL_E2E: late_join_spawn for {session.Name}");
            _ticker.MakeJoinGame(session, EntityUid.Invalid, silent: true);
        }
    }
}
