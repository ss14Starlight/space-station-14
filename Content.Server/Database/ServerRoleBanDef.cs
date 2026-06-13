using System.Linq;
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
        => new()
        {
            Id = serverRoleBan.Id,
            UserId = serverRoleBan.UserId,
            Address = serverRoleBan.Address == null ? null : new() { Address = serverRoleBan.Address.Value.address.ToString(), CidrMask = serverRoleBan.Address.Value.cidrMask },
            HWId = serverRoleBan.HWId == null ? null : new() { Hwid = serverRoleBan.HWId.Hwid.ToArray(), Type = (int)serverRoleBan.HWId.Type },
            BanTime = serverRoleBan.BanTime,
            ExpirationTime = serverRoleBan.ExpirationTime,
            RoundId = serverRoleBan.RoundId,
            PlayTimeAtNote = serverRoleBan.PlaytimeAtNote,
            Reason = serverRoleBan.Reason,
            Severity = serverRoleBan.Severity.ToString(),
            BanningAdmin = serverRoleBan.BanningAdmin,
            Unban = serverRoleBan.Unban == null ? [] : new() { serverRoleBan.Unban.ToNullLink() },
            Role = serverRoleBan.Role,
            ExemptFlags = null,
            ProjectName = serverRoleBan.ProjectName,
            ServerName = serverRoleBan.ServerName
        };
}

#endregion
