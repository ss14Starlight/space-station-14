using Content.Client.Actions;
using Content.Client.Administration.Systems;
using Content.Client.Ghost;
using Content.Client.Replay.Spectator;
using Content.Shared._Starlight.Replay;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Client.Player;
using Robust.Client.Replays.Playback;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Client._Starlight.Replay;

/// <summary>
/// Gives the client-spawned ReplayObserver a normal observer's abilities, since the server-side ghost setup never
/// runs for it. View settings live here because the observer is re-spawned whenever spectating ends.
/// </summary>
public sealed partial class ReplayObserverSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IReplayPlaybackManager _replayPlayback = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private AdminSystem _admin = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly EntProtoId _hearingActionProto = "ActionReplayToggleGhostHearing";
    private static readonly EntProtoId _playerOverlayActionProto = "ActionReplayTogglePlayerOverlay";
    private static readonly EntProtoId _statusIconsActionProto = "ActionReplayToggleStatusIcons";

    /// <summary>
    /// Deferred a frame so setup runs after GhostSystem's attach handling, which resets ghost visibility.
    /// </summary>
    private EntityUid? _pendingSetup;

    private EntityUid? _observer;

    private EntityUid? _overlayAction;

    private bool _drawFov;
    private bool _drawLight = true;
    private bool _personalLight;
    private bool _ghostsVisible = true;

    public bool IsReplayActive => _replayPlayback.Replay != null;

    public override void Initialize()
    {
        base.Initialize();

        // Replay playback only runs Update for systems that opt in to running outside prediction.
        UpdatesOutsidePrediction = true;

        SubscribeLocalEvent<ReplaySpectatorComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<ReplaySpectatorComponent, ToggleGhostsActionEvent>(OnToggleGhosts);
        SubscribeLocalEvent<ReplaySpectatorComponent, ToggleGhostHearingActionEvent>(OnToggleHearing);
        SubscribeLocalEvent<ReplaySpectatorComponent, ReplayTogglePlayerOverlayActionEvent>(OnTogglePlayerOverlay);
        SubscribeLocalEvent<ReplaySpectatorComponent, ReplayToggleStatusIconsActionEvent>(OnToggleStatusIcons);

        _replayPlayback.ReplayPlaybackStarted += OnPlaybackStarted;
        _replayPlayback.ReplayPlaybackStopped += OnPlaybackStopped;

        InitializeRoundSummary();
        InitializeViewer();
        InitializeLaws();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _replayPlayback.ReplayPlaybackStarted -= OnPlaybackStarted;
        _replayPlayback.ReplayPlaybackStopped -= OnPlaybackStopped;

        ShutdownRoundSummary();
        ShutdownViewer();
    }

    private void OnPlaybackStarted(MappingDataNode yamlMappingNode, List<object> objects) => ResetState();

    private void OnPlaybackStopped()
    {
        ShutdownPlayerOverlay();
        ResetActionsBarOffset();
        ResetState();
    }

    private void ResetState()
    {
        _pendingSetup = null;
        _observer = null;
        _overlayAction = null;
        _drawFov = false;
        _drawLight = true;
        _personalLight = false;
        _ghostsVisible = true;
        _hearAll = true;
        _overlayEnabled = false;
        _overlayShown = false;
        _overlayRefreshAccumulator = 0f;
        _statusIconsEnabled = true;
        _statusIconsShown = false;
        _statusIconsAction = null;
        _viewing.Clear();
        _radarAccumulator = 0f;
    }

    private void OnAttached(EntityUid uid, ReplaySpectatorComponent component, LocalPlayerAttachedEvent args)
    {
        // Spectated recorded mobs also get ReplaySpectatorComponent.
        if (!IsReplayActive || !IsClientSide(uid) || !HasComp<GhostComponent>(uid))
            return;

        _pendingSetup = uid;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!IsReplayActive)
            return;

        if (_pendingSetup is { } pending)
        {
            _pendingSetup = null;
            if (Exists(pending) && pending == _player.LocalEntity)
                SetupObserver(pending);
        }

        if (_observer is { } observer && observer == _player.LocalEntity && Exists(observer))
            SaveViewSettings(observer);

        UpdatePlayerOverlay(frameTime, IsHudHidden());
        UpdateStatusIcons();
        EnsureStatusIconOverlay();
        UpdateActionsBarOffset();
        UpdateRadar(frameTime);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!IsReplayActive)
            return;

        UpdateViewer();

        // Replay playback only updates systems that opt in to running outside prediction, and the UI system doesn't,
        // so no UI window would ever open or close. Run it here; live play updates it normally.
        _ui.Update(frameTime);
    }

    private void SetupObserver(EntityUid uid)
    {
        _observer = uid;

        if (TryComp<GhostComponent>(uid, out var ghost))
        {
            _actions.AddAction(uid, ref ghost.ToggleLightingActionEntity, ghost.ToggleLightingAction);
            _actions.AddAction(uid, ref ghost.ToggleFoVActionEntity, ghost.ToggleFoVAction);
            _actions.AddAction(uid, ref ghost.ToggleGhostsActionEntity, ghost.ToggleGhostsAction);

            // The regular hearing action is handled server-side.
            _actions.AddAction(uid, ref ghost.ToggleGhostHearingActionEntity, _hearingActionProto);
            _actions.SetToggled(ghost.ToggleGhostHearingActionEntity, !_hearAll);
        }

        _overlayAction = null;
        _actions.AddAction(uid, ref _overlayAction, _playerOverlayActionProto);
        _actions.SetToggled(_overlayAction, _overlayEnabled);

        _statusIconsAction = null;
        _actions.AddAction(uid, ref _statusIconsAction, _statusIconsActionProto);
        _actions.SetToggled(_statusIconsAction, _statusIconsEnabled);
        _statusIconsShown = false; // A fresh observer has none of the HUD components yet.

        ApplyViewSettings(uid);
        _ghost.ToggleGhostVisibility(_ghostsVisible);

        _actions.LinkAllActions();
    }

    private void ApplyViewSettings(EntityUid uid)
    {
        if (TryComp<EyeComponent>(uid, out var eye))
        {
            _eye.SetDrawFov(uid, _drawFov, eye);
            _eye.SetDrawLight((uid, eye), _drawLight);
        }

        if (_lights.TryGetLight(uid, out var light))
            _lights.SetEnabled(uid, _personalLight, light);
    }

    private void SaveViewSettings(EntityUid uid)
    {
        if (TryComp<EyeComponent>(uid, out var eye))
        {
            _drawFov = eye.DrawFov;
            _drawLight = eye.DrawLight;
        }

        if (_lights.TryGetLight(uid, out var light))
            _personalLight = light.Enabled;
    }

    private void OnToggleGhosts(EntityUid uid, ReplaySpectatorComponent component, ToggleGhostsActionEvent args)
    {
        // GhostSystem does the toggle and ignores handled events, so leave this unhandled.
        if (!IsReplayActive || uid != _observer)
            return;

        _ghostsVisible = !_ghostsVisible;
        _overlayRefreshAccumulator = OverlayRefreshInterval; // Refresh labels next frame.
    }
}
