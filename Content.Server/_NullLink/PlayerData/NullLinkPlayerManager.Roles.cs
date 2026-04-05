using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared._NullLink;
using Robust.Shared.Player;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public ValueTask SyncRoles(PlayerRolesSyncEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;
        playerData.SyncRoles(ev);
        playerData.DiscordId = ev.DiscordId;

        MentorCheck(ev.Player, playerData);
        AdminCheck(ev.Player, playerData);

        RebuildTitle(ev.Player, playerData);

        SendPlayerRoles(playerData.Session, playerData.Roles);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateRoles(RolesChangedEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;
        playerData.UpdateRoles(ev);
        playerData.DiscordId = ev.DiscordId;

        MentorCheck(ev.Player, playerData);
        AdminCheck(ev.Player, playerData);

        RebuildTitle(ev.Player, playerData);

        SendPlayerRoles(playerData.Session, playerData.Roles);
        return ValueTask.CompletedTask;
    }

    private void SendPlayerRoles(ICommonSession session, ImmutableHashSet<ulong> roles)
    => _netMgr.ServerSendMessage(new MsgUpdatePlayerRoles
    {
        Roles = roles,
        DiscordLink = GetDiscordAuthUrl(session.UserId.ToString())
    }, session.Channel);
}
