using System.Linq;
using System.Net;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Starlight.NullLink;


namespace Content.Server.Database
{
    public sealed class ServerBanDef
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
        public ServerUnbanDef? Unban { get; }
        public ServerBanExemptFlags ExemptFlags { get; }

        public string? ProjectName { get; } // Starlight-edit

        public string? ServerName { get; } // Starlight-edit

        public bool Network { get; } // Starlight-edit

        public ServerBanDef(int? id,
            NetUserId? userId,
            (IPAddress, int)? address,
            TypedHwid? hwId,
            DateTimeOffset banTime,
            DateTimeOffset? expirationTime,
            int? roundId,
            TimeSpan playtimeAtNote,
            string reason,
            NoteSeverity severity,
            NetUserId? banningAdmin,
            ServerUnbanDef? unban,
            ServerBanExemptFlags exemptFlags = default,
            string? projectName = null,
            string? serverName = null,
            bool network = false)
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
            ExemptFlags = exemptFlags;
            ProjectName = projectName;
            ServerName = serverName;
            Network = network;
        }

        public string FormatBanMessage(IConfigurationManager cfg, ILocalizationManager loc)
        {
            string expires;
            if (ExpirationTime is { } expireTime)
            {
                var duration = expireTime - BanTime;
                var utc = expireTime.ToUniversalTime();
                expires = loc.GetString("ban-expires", ("duration", duration.TotalMinutes.ToString("N0")), ("time", utc.ToString("f")));
            }
            else
            {
                var appeal = cfg.GetCVar(CCVars.InfoLinksAppeal);
                expires = !string.IsNullOrWhiteSpace(appeal)
                    ? loc.GetString("ban-banned-permanent-appeal", ("link", appeal))
                    : loc.GetString("ban-banned-permanent");
            }

            // Starlight Start: Player facing Ban ID && Server/Project Names
            var banIdLine = Id is { } banId
                ? $"{loc.GetString("ban-banned-id", ("id", banId))}\n"
                : string.Empty;

            string serverProjectLine;
            if (ProjectName == null)
                serverProjectLine = string.Empty;
            else if (ServerName == null)
                serverProjectLine = $"{loc.GetString("ban-project", ("project", ProjectName ?? ""))}\n";
            else
                serverProjectLine = $"{loc.GetString("ban-project-server", ("project", ProjectName ?? ""), ("server", ServerName ?? ""))}\n";
            // Starlight End

            // Starlight edit Start: Added banIdLine
            return $"""
                   {loc.GetString("ban-banned-1")}
                   {loc.GetString("ban-banned-2", ("reason", Reason))}
                   {banIdLine}{expires}
                   {serverProjectLine}{loc.GetString("ban-banned-3")}
                   """;
            // Starlight edit End
        }
    }

    #region Starlight

    public static class BanDefExtensions
    {
        public static AdminBan ToNullLink(this ServerBanDef banDef)
            => new()
            {
                Id = banDef.Id,
                UserId = banDef.UserId,
                Address = banDef.Address == null ? null : new() { Address = banDef.Address.Value.address.ToString(), CidrMask = banDef.Address.Value.cidrMask },
                HWId = banDef.HWId == null ? null : new() { Hwid = banDef.HWId.Hwid.ToArray(), Type = (int)banDef.HWId.Type },
                BanTime = banDef.BanTime,
                ExpirationTime = banDef.ExpirationTime,
                RoundId = banDef.RoundId,
                PlayTimeAtNote = banDef.PlaytimeAtNote,
                Reason = banDef.Reason,
                Severity = banDef.Severity.ToString(),
                BanningAdmin = banDef.BanningAdmin,
                Unban = banDef.Unban == null ? [] : new() { banDef.Unban.ToNullLink() },
                Role = null,
                ExemptFlags = (int)banDef.ExemptFlags,
                ProjectName = banDef.ProjectName,
                ServerName = banDef.ServerName,

            };
    }

    #endregion
}
