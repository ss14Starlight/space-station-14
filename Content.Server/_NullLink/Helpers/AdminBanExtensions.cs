using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Shared.Network;
using Starlight.NullLink;

namespace Content.Server._NullLink.Helpers;

public static class AdminBanExtensions
{
    public static ServerBanDef ToDef(this AdminBan ban)
        => new(ban.Id, ban.UserId == null ? null : new NetUserId(ban.UserId.Value), ban.Address == null ? null : (IPAddress.Parse(ban.Address.Address), ban.Address.CidrMask), ban.HWId == null ? null : new ImmutableTypedHwid(ban.HWId.Hwid.ToImmutableArray(), (HwidType)ban.HWId.Type), ban.BanTime, ban.ExpirationTime, ban.RoundId, ban.PlayTimeAtNote, ban.Reason, Enum.Parse<NoteSeverity>(ban.Severity), ban.BanningAdmin == null ? null : new NetUserId(ban.BanningAdmin.Value), null, ban.ExemptFlags == null ? 0 : (ServerBanExemptFlags)ban.ExemptFlags.Value, ban.ProjectName, ban.ServerName, true);

    public static IEnumerable<ServerBanDef> ToDef(this IEnumerable<AdminBan> bans)
        => bans.Select(b => b.ToDef());

    public static ServerRoleBanDef ToRoleDef(this AdminBan ban) 
        => new(ban.Id, ban.UserId == null ? null : new NetUserId(ban.UserId.Value), ban.Address == null ? null : (IPAddress.Parse(ban.Address.Address), ban.Address.CidrMask), ban.HWId == null ? null : new ImmutableTypedHwid(ban.HWId.Hwid.ToImmutableArray(), (HwidType)ban.HWId.Type), ban.BanTime, ban.ExpirationTime, ban.RoundId, ban.PlayTimeAtNote, ban.Reason, Enum.Parse<NoteSeverity>(ban.Severity), ban.BanningAdmin == null ? null : new NetUserId(ban.BanningAdmin.Value), null, ban.Role ?? "", ban.ProjectName, ban.ServerName);

    public static IEnumerable<ServerRoleBanDef> ToRoleDef(this IEnumerable<AdminBan> bans)
        => bans.Select(b => b.ToRoleDef());
}
