using Content.Client.Replay.Spectator;
using Content.Shared._Starlight.Replay;
using Content.Shared.Administration;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.Network;

namespace Content.Client._Starlight.Replay;

// The admin player list isn't recorded, but minds are, so the overlay's list is rebuilt from them.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedRoleSystem _roles = default!;

    private const float OverlayRefreshInterval = 1f;

    private bool _overlayEnabled;
    private bool _overlayShown;
    private float _overlayRefreshAccumulator;

    private void OnTogglePlayerOverlay(EntityUid uid, ReplaySpectatorComponent component, ReplayTogglePlayerOverlayActionEvent args)
    {
        if (args.Handled || !IsReplayActive)
            return;

        args.Handled = true;
        _overlayEnabled = !_overlayEnabled;
        _actions.SetToggled(_overlayAction, _overlayEnabled);

        var msg = _overlayEnabled
            ? Loc.GetString("replay-observer-player-overlay-on")
            : Loc.GetString("replay-observer-player-overlay-off");
        _popup.PopupEntity(msg, uid);
    }

    private void ShutdownPlayerOverlay()
    {
        if (_overlayShown)
            _admin.AdminOverlayOff();

        _overlayShown = false;
        _admin.SetPlayerList(null);
        _admin.HideOverlayPlaytime = false;
    }

    private void UpdatePlayerOverlay(float frameTime, bool hudHidden)
    {
        var show = _overlayEnabled && !hudHidden;
        if (show != _overlayShown)
        {
            _overlayShown = show;
            _overlayRefreshAccumulator = 0f;

            if (show)
            {
                _admin.HideOverlayPlaytime = true;
                RefreshPlayerList();
                _admin.AdminOverlayOn();
            }
            else
            {
                _admin.AdminOverlayOff();
            }
        }

        if (!_overlayShown)
            return;

        _overlayRefreshAccumulator += frameTime;
        if (_overlayRefreshAccumulator < OverlayRefreshInterval)
            return;

        _overlayRefreshAccumulator = 0f;
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        var players = new Dictionary<NetUserId, PlayerInfo>();

        var query = AllEntityQuery<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mind.UserId is not { } userId)
                continue;

            _player.TryGetSessionById(userId, out var session);
            var entity = session?.AttachedEntity ?? mind.CurrentEntity;

            if (entity is not { } ent || !Exists(ent))
                continue;

            // No label for anyone not drawn, e.g. ghosts while ghost visibility is off.
            if (TryComp<SpriteComponent>(ent, out var sprite) && !sprite.Visible)
                continue;

            // Prefer the mind driving the user's current entity.
            if (players.ContainsKey(userId) && mind.CurrentEntity != ent)
                continue;

            var roleComp = _roles.GetRoleCompByTime(mind);
            var job = _jobs.MindTryGetJob(mindId, out var jobProto) ? jobProto.LocalizedName : string.Empty;
            var connected = session != null && session.Status is SessionStatus.Connected or SessionStatus.InGame;

            players[userId] = new PlayerInfo(
                session?.Name ?? Loc.GetString("generic-unknown-title"),
                Name(ent),
                Identity.Name(ent, EntityManager),
                job,
                _roles.MindIsAntagonist(mindId),
                mind.RoleType,
                roleComp?.Comp.Subtype,
                roleComp?.Comp.SortWeight ?? 0,
                GetNetEntity(ent),
                userId,
                connected,
                true,
                null,
                jobProto?.ID);
        }

        _admin.SetPlayerList(players.Values);
    }
}
