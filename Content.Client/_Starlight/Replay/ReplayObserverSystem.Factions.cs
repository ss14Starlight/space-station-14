using Content.Client.Overlays;
using Content.Client.Replay.Spectator;
using Content.Shared._Starlight.Replay;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Replay;

// Faction icons (nukie S, revs, cults...), off by default. StatusIconSystem.Starlight hides them from the observer
// unless this is on, including those normally visible to ghosts.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private ShowSyndicateIconsSystem _syndicateIcons = default!;

    private static readonly EntProtoId _factionHudProto = "ReplayObserverFactionHud";
    private static readonly EntProtoId _factionIconsActionProto = "ActionReplayToggleFactionIcons";

    private bool _factionIconsEnabled;
    private bool _factionIconsShown;
    private EntityUid? _factionIconsAction;

    /// <summary>
    /// Whether faction icons show for <paramref name="viewer"/>, or null if it isn't the replay observer.
    /// </summary>
    public bool? FactionIconsVisibleTo(EntityUid? viewer) =>
        IsReplayActive && viewer != null && viewer == _observer ? _factionIconsEnabled : null;

    [SubscribeLocalEvent]
    private void OnToggleFactionIcons(EntityUid uid, ReplaySpectatorComponent component, ReplayToggleFactionIconsActionEvent args)
    {
        if (args.Handled || !IsReplayActive)
            return;

        args.Handled = true;
        _factionIconsEnabled = !_factionIconsEnabled;
        _actions.SetToggled(_factionIconsAction, _factionIconsEnabled);

        var msg = _factionIconsEnabled
            ? Loc.GetString("replay-observer-faction-icons-on")
            : Loc.GetString("replay-observer-faction-icons-off");
        _popup.PopupEntity(msg, uid);
    }

    private void AddFactionIconsAction(EntityUid observer)
    {
        _factionIconsAction = null;
        _actions.AddAction(observer, ref _factionIconsAction, _factionIconsActionProto);
        _actions.SetToggled(_factionIconsAction, _factionIconsEnabled);
        _factionIconsShown = false; // A fresh observer has none of the HUD components yet.
    }

    // Adds the components that make some factions add their icons at all, such as the nukie S.
    private void UpdateFactionIcons()
    {
        if (_observer is not { } observer || observer != _player.LocalEntity || !Exists(observer))
            return;

        if (_factionIconsEnabled == _factionIconsShown)
            return;

        _factionIconsShown = _factionIconsEnabled;
        var components = _proto.Index(_factionHudProto).Components;

        if (_factionIconsShown)
        {
            EntityManager.AddComponents(observer, components, removeExisting: false);
            return;
        }

        EntityManager.RemoveComponents(observer, components);

        // EquipmentHudSystem refreshes during ComponentRemove, while the component still counts as present.
        _syndicateIcons.Deactivate();
    }
}
