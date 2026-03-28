using System.Net;
using Content.Shared.Database;
using Robust.Shared.Network;
using Starlight.NullLink;

namespace Content.Server.Database;

public sealed class ServerRoleBanDef
{
    public int? Id { get; }
    public NetUserId? UserId { get; }
    public (IPAddress address, int cidrMask)? Address { get; }
    public ImmutableTypedHwid? HWId { get; }

    public DateTimeOffset BanTime { get; }
    public DateTimeOffset? ExpirationTime { get; }
    public int? RoundId { get; }
    public TimeSpan PlaytimeAtNote { get; }
    public string Reason { get; }
    public NoteSeverity Severity { get; set; }
    public NetUserId? BanningAdmin { get; }
    public ServerRoleUnbanDef? Unban { get; }
    public string Role { get; }

    public string? ProjectName { get; }

    public string? ServerName { get; }

    public ServerRoleBanDef(
        int? id,
        NetUserId? userId,
        (IPAddress, int)? address,
        ImmutableTypedHwid? hwId,
        DateTimeOffset banTime,
        DateTimeOffset? expirationTime,
        int? roundId,
        TimeSpan playtimeAtNote,
        string reason,
        NoteSeverity severity,
        NetUserId? banningAdmin,
        ServerRoleUnbanDef? unban,
        string role,
        string? projectName = null,
        string? serverName = null)
    {
        if (userId == null && address == null && hwId ==  null)
        {
            throw new ArgumentException("Must have at least one of banned user, banned address or hardware ID");
        }

        if (address is {} addr && addr.Item1.IsIPv4MappedToIPv6)
        {
            // Fix IPv6-mapped IPv4 addresses
            // So that IPv4 addresses are consistent between separate-socket and dual-stack socket modes.
            address = (addr.Item1.MapToIPv4(), addr.Item2 - 96);
        }

        Id = id;
        UserId = userId;
        Address = address;
        HWId = hwId;
        BanTime = banTime;
        ExpirationTime = expirationTime;
        RoundId = roundId;
        PlaytimeAtNote = playtimeAtNote;
        Reason = reason;
        Severity = severity;
        BanningAdmin = banningAdmin;
        Unban = unban;
        Role = role;
        ProjectName = projectName;
        ServerName = serverName;
    }
}

#region Starlight

public static class RoleBanDefExtensions
{
    public static AdminBan ToNullLink(this ServerRoleBanDef serverRoleBan)
        => new(serverRoleBan.Id, serverRoleBan.UserId, serverRoleBan.Address, serverRoleBan.HWId == null ? null : (serverRoleBan.HWId.Hwid, (int)serverRoleBan.HWId.Type), serverRoleBan.BanTime, serverRoleBan.ExpirationTime, serverRoleBan.RoundId, serverRoleBan.PlaytimeAtNote, serverRoleBan.Reason, serverRoleBan.Severity.ToString(), serverRoleBan.BanningAdmin, serverRoleBan.Unban == null ?[] : new() { serverRoleBan.Unban.ToNullLink() }, serverRoleBan.Role, null, serverRoleBan.ProjectName, serverRoleBan.ServerName);
}

#endregion