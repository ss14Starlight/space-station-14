using Content.Client.Replay.Spectator;
using Content.Shared._Starlight.Replay;
using Content.Shared.Administration;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Network;

namespace Content.Client._Starlight.Replay;

// The admin player list isn't recorded, but minds are, so the overlay's list is rebuilt from them.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedRoleSystem _roles = default!;

    private const float OverlayRefreshInterval = 1f;

    private bool _overlayEnabled;
    private float _overlayRefreshAccumulator;

    private void OnTogglePlayerOverlay(EntityUid uid, ReplaySpectatorComponent component, ReplayTogglePlayerOverlayActionEvent args)
    {
        if (args.Handled || !IsReplayActive)
            return;

        args.Handled = true;
        SetPlayerOverlay(!_overlayEnabled);
        _actions.SetToggled(_overlayAction, _overlayEnabled);

        var msg = _overlayEnabled
            ? Loc.GetString("replay-observer-player-overlay-on")
            : Loc.GetString("replay-observer-player-overlay-off");
        _popup.PopupEntity(msg, uid);
    }

    private void SetPlayerOverlay(bool enabled)
    {
        _overlayEnabled = enabled;
        _overlayRefreshAccumulator = 0f;

        if (enabled)
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

    private void ShutdownPlayerOverlay()
    {
        if (_overlayEnabled)
            _admin.AdminOverlayOff();

        _admin.SetPlayerList(null);
        _admin.HideOverlayPlaytime = false;
    }

    private void UpdatePlayerOverlay(float frameTime)
    {
        if (!_overlayEnabled)
            return;

        _overlayRefreshAccumulator += frameTime;
        if (_overlayRefreshAccumulator < OverlayRefreshInterval)
            return;

        _overlayRefreshAccumulator = 0f;
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        UpdateMindJobPresence();

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

            // Prefer the mind driving the user's current entity.
            if (players.ContainsKey(userId) && mind.CurrentEntity != ent)
                continue;

            var roleComp = _roles.GetRoleCompByTime(mind);
            TryGetReplayJobName(ent, mindId, out var job);
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
                null);
        }

        _admin.SetPlayerList(players.Values);
    }
}
