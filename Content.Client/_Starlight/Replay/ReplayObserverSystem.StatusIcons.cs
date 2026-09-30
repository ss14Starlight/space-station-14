using Content.Client.Overlays;
using Content.Client.Replay.Spectator;
using Content.Shared._Starlight.Replay;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Replay;

// Job, mindshield and health HUDs, as aghosts have. Components come from a prototype so the HUD config lives in YAML.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ShowJobIconsSystem _jobIcons = default!;
    [Dependency] private ShowMindShieldIconsSystem _mindShieldIcons = default!;
    [Dependency] private ShowHealthBarsSystem _healthBars = default!;
    [Dependency] private ShowHealthIconsSystem _healthIcons = default!;

    private static readonly EntProtoId StatusHudComponents = "ReplayObserverStatusHud";

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

    private void UpdateStatusIcons(bool hudHidden)
    {
        if (_observer is not { } observer || observer != _player.LocalEntity || !Exists(observer))
            return;

        var show = _statusIconsEnabled && !hudHidden;
        if (show == _statusIconsShown)
            return;

        _statusIconsShown = show;
        var components = _proto.Index(StatusHudComponents).Components;

        if (show)
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
