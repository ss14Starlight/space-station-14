using Content.Client.Overlays;
using Content.Client.Replay.Spectator;
using Content.Client.StatusIcon;
using Content.Shared._Starlight.Replay;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Replay;

// Job, mindshield and health HUDs, as aghosts have. Components come from a prototype so the HUD config lives in YAML.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private ShowJobIconsSystem _jobIcons = default!;
    [Dependency] private ShowMindShieldIconsSystem _mindShieldIcons = default!;
    [Dependency] private ShowHealthBarsSystem _healthBars = default!;
    [Dependency] private ShowHealthIconsSystem _healthIcons = default!;

    private static readonly EntProtoId _statusHudProto = "ReplayObserverStatusHud";

    private bool _statusIconsEnabled = true;
    private bool _statusIconsShown;
    private EntityUid? _statusIconsAction;

    private void OnToggleStatusIcons(EntityUid uid, ReplaySpectatorComponent component, ReplayToggleStatusIconsActionEvent args)
    {
        if (args.Handled || !IsReplayActive)
            return;

        args.Handled = true;
        _statusIconsEnabled = !_statusIconsEnabled;
        _actions.SetToggled(_statusIconsAction, _statusIconsEnabled);

        var msg = _statusIconsEnabled
            ? Loc.GetString("replay-observer-status-icons-on")
            : Loc.GetString("replay-observer-status-icons-off");
        _popup.PopupEntity(msg, uid);
    }

    // StatusIconSystem toggles its overlay on any cvar change event rather than syncing to the cvars, and replay
    // loading fires one while the overlay is up, removing it. Keep it present while both cvars say it should be.
    private void EnsureStatusIconOverlay()
    {
        if (!_cfg.GetCVar(CCVars.GlobalStatusIconsEnabled) || !_cfg.GetCVar(CCVars.LocalStatusIconsEnabled))
            return;

        if (!_overlayManager.HasOverlay<StatusIconOverlay>())
            _overlayManager.AddOverlay(new StatusIconOverlay());
    }

    private void UpdateStatusIcons()
    {
        if (_observer is not { } observer || observer != _player.LocalEntity || !Exists(observer))
            return;

        if (_statusIconsEnabled == _statusIconsShown)
            return;

        _statusIconsShown = _statusIconsEnabled;
        var components = _proto.Index(_statusHudProto).Components;

        if (_statusIconsShown)
        {
            EntityManager.AddComponents(observer, components, removeExisting: false);
            return;
        }

        EntityManager.RemoveComponents(observer, components);

        // EquipmentHudSystem refreshes during ComponentRemove, while the component still counts as present.
        _jobIcons.Deactivate();
        _mindShieldIcons.Deactivate();
        _healthBars.Deactivate();
        _healthIcons.Deactivate();
    }
}
