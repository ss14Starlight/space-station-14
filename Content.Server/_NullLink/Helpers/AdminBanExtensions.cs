using System.Linq;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Shared.Network;
using Starlight.NullLink;

namespace Content.Server._NullLink.Helpers;

public static class AdminBanExtensions
{
    public static ServerBanDef ToDef(this AdminBan ban)
        => new(ban.Id, ban.UserId == null ? null : new NetUserId(ban.UserId.Value), ban.Address, ban.HWId == null ? null : new ImmutableTypedHwid(ban.HWId.Value.hwid, (HwidType)ban.HWId.Value.type), ban.BanTime, ban.ExpirationTime, ban.RoundId, ban.PlayTimeAtNote, ban.Reason, Enum.Parse<NoteSeverity>(ban.Severity), ban.BanningAdmin == null ? null : new NetUserId(ban.BanningAdmin.Value), null, ban.ExemptFlags == null ? 0 : (ServerBanExemptFlags)ban.ExemptFlags.Value, ban.ProjectName, ban.ServerName);

    public static IEnumerable<ServerBanDef> ToDef(this IEnumerable<AdminBan> bans)
        => bans.Select(b => b.ToDef());

    public static ServerRoleBanDef ToRoleDef(this AdminBan ban) 
        => new(ban.Id, ban.UserId == null ? null : new NetUserId(ban.UserId.Value), ban.Address, ban.HWId == null ? null : new ImmutableTypedHwid(ban.HWId.Value.hwid, (HwidType)ban.HWId.Value.type), ban.BanTime, ban.ExpirationTime, ban.RoundId, ban.PlayTimeAtNote, ban.Reason, Enum.Parse<NoteSeverity>(ban.Severity), ban.BanningAdmin == null ? null : new NetUserId(ban.BanningAdmin.Value), null, ban.Role ?? "", ban.ProjectName, ban.ServerName);

    public static IEnumerable<ServerRoleBanDef> ToRoleDef(this IEnumerable<AdminBan> bans)
        => bans.Select(b => b.ToRoleDef());
}