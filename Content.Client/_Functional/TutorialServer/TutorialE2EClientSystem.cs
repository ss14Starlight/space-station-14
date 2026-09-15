using Content.Shared._Functional.TutorialServer;
using Content.Shared.GameTicking;
using Robust.Client.Console;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Functional.TutorialServer;

/// <summary>
/// E2E helper: auto-ready in lobby when <see cref="TutorialCVars.E2EAutoReady"/> is enabled.
/// </summary>
public sealed partial class TutorialE2EClientSystem : EntitySystem
{
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool _readySent;
    private TimeSpan _nextAttempt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TickerJoinLobbyEvent>(_ =>
        {
            _readySent = false;
            // Allow lobby UI a beat to exist, then spam ready until accepted.
            _nextAttempt = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_cfg.GetCVar(TutorialCVars.E2EAutoReady) || _readySent)
            return;

        if (_timing.CurTime < _nextAttempt)
            return;

        // Prefer LobbyState, but still try once connected if lobby state is delayed.
        if (_state.CurrentState is not Content.Client.Lobby.LobbyState)
        {
            _nextAttempt = _timing.CurTime + TimeSpan.FromSeconds(0.5);
            return;
        }

        _console.ExecuteCommand("toggleready true");
        _readySent = true;
        Log.Info("TUTORIAL_E2E: auto_ready sent");
    }
}
