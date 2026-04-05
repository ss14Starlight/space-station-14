using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Content.Server.Database;
using Content.Shared.Administration.Notes;
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

    public static SharedAdminNote? ToNote(this AdminBan ban)
        => ban.UserId == null || ban.Id == null ? null : new(ban.Id.Value, new NetUserId(ban.UserId.Value), ban.RoundId, ban.ServerName, ban.ProjectName, ban.PlayTimeAtNote, NoteType.ServerBan, ban.Reason, Enum.Parse<NoteSeverity>(ban.Severity), false, "", "", ban.BanTime.DateTime, null, ban.ExpirationTime?.DateTime, null, null, null, null, true);

    public static Dictionary<(int, NoteType), SharedAdminNote> ToNoteDef(this IEnumerable<AdminBan> ban)
        => ban.Select(b => b.ToNote()).OfType<SharedAdminNote>().ToDictionary(n => (n.Id, n.NoteType));
}
