using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server._NullLink.Core;
using Content.Server._NullLink.Helpers;
using Content.Server.Database;
using Content.Shared._NullLink;
using Content.Shared.NullLink.CCVar;
using Content.Shared.Starlight;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Starlight.NullLink;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    public ValueTask SyncRoles(PlayerRolesSyncEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;
        playerData.Roles.Clear();
        playerData.Roles.UnionWith(ev.Roles);
        playerData.DiscordId = ev.DiscordId;

        MentorCheck(ev.Player, playerData);

        RebuildTitle(ev.Player, playerData);

        SendPlayerRoles(playerData.Session, playerData.Roles);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateRoles(RolesChangedEvent ev)
    {
        if (!_playerById.TryGetValue(ev.Player, out var playerData))
            return ValueTask.CompletedTask;
        playerData.Roles.ExceptWith(ev.Remove);
        playerData.Roles.UnionWith(ev.Add);
        playerData.DiscordId = ev.DiscordId;

        MentorCheck(ev.Player, playerData);

        RebuildTitle(ev.Player, playerData);

        SendPlayerRoles(playerData.Session, playerData.Roles);
        return ValueTask.CompletedTask;
    }

    private void SendPlayerRoles(ICommonSession session, HashSet<ulong> roles)
    => _netMgr.ServerSendMessage(new MsgUpdatePlayerRoles
    {
        Roles = roles,
        DiscordLink = GetDiscordAuthUrl(session.UserId.ToString())
    }, session.Channel);
}
