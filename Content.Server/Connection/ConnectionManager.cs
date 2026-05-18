using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Connection.IPIntel;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Timing;

#region Starlight
using Content.Server._NullLink.Core;
using Content.Server._NullLink.PlayerData;
using Content.Server._Starlight.Connection;
using Content.Server.Discord.DiscordLink;
using Content.Shared._NullLink;
using Content.Shared.NullLink.CCVar;
using Content.Shared.Starlight;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Utility;
#endregion Starlight

/*
 * TODO: Remove baby jail code once a more mature gateway process is established. This code is only being issued as a stopgap to help with potential tiding in the immediate future.
 */

namespace Content.Server.Connection
{
    public interface IConnectionManager
    {
        void Initialize();
        void PostInit();

        /// <summary>
        /// Temporarily allow a user to bypass regular connection requirements.
        /// </summary>
        /// <remarks>
        /// The specified user will be allowed to bypass regular player cap,
        /// whitelist and panic bunker restrictions for <paramref name="duration"/>.
        /// Bans are not bypassed.
        /// </remarks>
        /// <param name="user">The user to give a temporary bypass.</param>
        /// <param name="duration">How long the bypass should last for.</param>
        void AddTemporaryConnectBypass(NetUserId user, TimeSpan duration);

        void Update();

        /// <summary>
        /// Gets the resolved real client IP for a connected user.
        /// When conntrack resolution is active, this returns the real IP behind SNAT.
        /// Returns <c>null</c> if the user has no cached address.
        /// </summary>
        IPAddress? GetResolvedAddress(NetUserId user); // Starlight
    }

    /// <summary>
    ///     Handles various duties like guest username assignment, bans, connection logs, etc...
    /// </summary>
    public sealed partial class ConnectionManager : IConnectionManager
    {
        [Dependency] private readonly IActorRouter _actors = default!; // NullLink
        [Dependency] private readonly INullLinkPlayerManager _nullLinkPlayerManager = default!; // NullLink
        [Dependency] private readonly IBanManager _banManager = default!; // NullLink-edit: move to general method at Manager
        [Dependency] private readonly IPlayerManager _plyMgr = default!;
        [Dependency] private readonly IServerNetManager _netMgr = default!;
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly ILocalizationManager _loc = default!;
        [Dependency] private readonly ServerDbEntryManager _serverDbEntry = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly IHttpClientHolder _http = default!;
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private GameTicker? _ticker;

        private ISawmill _sawmill = default!;
        private readonly Dictionary<NetUserId, TimeSpan> _temporaryBypasses = [];
        private readonly Dictionary<NetUserId, IPAddress> _resolvedAddresses = []; // Starlight
        private IPIntel.IPIntel _ipintel = default!;
        private ConntrackResolver _conntrack = default!; // Starlight

        // nulllink start
        private RoleRequirementPrototype? _bunkerBypass;
        private ServerPlaytimeRecognitionPrototype? _serverPlaytimeRecognition;
        private string? _project;
        private string? _server;
        // nulllink end

        public void PostInit()
        {
            InitializeWhitelist();

            _conntrack = new ConntrackResolver(_http, _cfg, _logManager); // Starlight
            // NullLink start
            _cfg.OnValueChanged(NullLinkCCVars.Project, x => _project = x, true);
            _cfg.OnValueChanged(NullLinkCCVars.Server, x => _server = x, true);
            _cfg.OnValueChanged(NullLinkCCVars.BunkerBypass, reqProtoId
                => _bunkerBypass = _prototypeManager.TryIndex<RoleRequirementPrototype>(reqProtoId, out var proto) ? proto : null, true);
            // NullLink end
        }

        public void Initialize()
        {
            _sawmill = _logManager.GetSawmill("connections");

            _ipintel = new IPIntel.IPIntel(new IPIntelApi(_http, _cfg), _db, _cfg, _logManager, _chatManager, _gameTiming);

            _netMgr.Connecting += NetMgrOnConnecting;
            _netMgr.AssignUserIdCallback = AssignUserIdCallback;
            _plyMgr.PlayerStatusChanged += PlayerStatusChanged;
            // Approval-based IP bans disabled because they don't play well with Happy Eyeballs.
            // _netMgr.HandleApprovalCallback = HandleApproval;
        }

        public void AddTemporaryConnectBypass(NetUserId user, TimeSpan duration)
        {
            ref var time = ref CollectionsMarshal.GetValueRefOrAddDefault(_temporaryBypasses, user, out _);
            var newTime = _gameTiming.RealTime + duration;
            // Make sure we only update the time if we wouldn't shrink it.
            if (newTime > time)
                time = newTime;
        }

        // Starlight: resolved IP cache
        public IPAddress? GetResolvedAddress(NetUserId user)
        {
            return _resolvedAddresses.GetValueOrDefault(user);
        }

        public async void Update()
        {
            try
            {
                await _ipintel.Update();
            }
            catch (Exception e)
            {
                _sawmill.Error("IPIntel update failed:" + e);
            }
        }

        /*
        private async Task<NetApproval> HandleApproval(NetApprovalEventArgs eventArgs)
        {
            var ban = await _db.GetServerBanByIpAsync(eventArgs.Connection.RemoteEndPoint.Address);
            if (ban != null)
            {
                var expires = Loc.GetString("ban-banned-permanent");
                if (ban.ExpirationTime is { } expireTime)
                {
                    var duration = expireTime - ban.BanTime;
                    var utc = expireTime.ToUniversalTime();
                    expires = Loc.GetString("ban-expires", ("duration", duration.TotalMinutes.ToString("N0")), ("time", utc.ToString("f")));
                }
                var reason = Loc.GetString("ban-banned-1") + "\n" + Loc.GetString("ban-banned-2", ("reason", this.Reason)) + "\n" + expires;;
                return NetApproval.Deny(reason);
            }

            return NetApproval.Allow();
        }
        */

        private async Task NetMgrOnConnecting(NetConnectingArgs e)
        {
            // Starlight: resolve real client IP via conntrack-agent (SNAT bypass)
            var addr = await _conntrack.ResolveRealIp(e.IP) ?? e.IP.Address;

            var deny = await ShouldDeny(e, addr); // Starlight
            var userId = e.UserId;

            var serverId = (await _serverDbEntry.ServerEntity).Id;

            var hwid = e.UserData.GetModernHwid();
            var trust = e.UserData.Trust;

            if (deny != null)
            {
                var (reason, msg, banHits) = deny.Value;

                var id = await _db.AddConnectionLogAsync(userId, e.UserName, addr, hwid, trust, reason, serverId);
                if (banHits is { Count: > 0 })
                    await _db.AddServerBanHitsAsync(id, banHits);

                var properties = new Dictionary<string, object>();
                if (reason == ConnectionDenyReason.Full)
                    properties["delay"] = _cfg.GetCVar(CCVars.GameServerFullReconnectDelay);

                //NullLink discord link
                properties["discord"] = _nullLinkPlayerManager.GetDiscordAuthUrl(e.UserId.ToString());

                e.Deny(new NetDenyReason(msg, properties));
            }
            else
            {
                _resolvedAddresses[userId] = addr; // Starlight: cache resolved IP for later lookups
                await _db.AddConnectionLogAsync(userId, e.UserName, addr, hwid, trust, null, serverId);

                if (!ServerPreferencesManager.ShouldStorePrefs(e.AuthType))
                    return;

                await _db.UpdatePlayerRecordAsync(userId, e.UserName, addr, hwid);
            }
        }

        private async void PlayerStatusChanged(object? sender, SessionStatusEventArgs args)
        {
            if (args.NewStatus == SessionStatus.Connected)
            {
                AdminAlertIfSharedConnection(args.Session);
            }
            else if (args.NewStatus == SessionStatus.Disconnected) // Starlight
                _resolvedAddresses.Remove(args.Session.UserId); // Starlight
        }

        private void AdminAlertIfSharedConnection(ICommonSession newSession)
        {
            var playerThreshold = _cfg.GetCVar(CCVars.AdminAlertMinPlayersSharingConnection);
            if (playerThreshold < 0)
                return;

            var addr = _resolvedAddresses.GetValueOrDefault(newSession.UserId)
                       ?? newSession.Channel.RemoteEndPoint.Address; // Starlight: use resolved IP

            var otherConnectionsFromAddress = _plyMgr.Sessions.Where(session =>
                    session.Status is SessionStatus.Connected or SessionStatus.InGame
                    && (_resolvedAddresses.GetValueOrDefault(session.UserId)
                        ?? session.Channel.RemoteEndPoint.Address).Equals(addr) // Starlight: use resolved IP
                    && session.UserId != newSession.UserId)
                .ToList();

            var otherConnectionCount = otherConnectionsFromAddress.Count;
            if (otherConnectionCount + 1 < playerThreshold) // Add one for the total, not just others, using the address
                return;

            var username = newSession.Name;
            var otherUsernames = string.Join(", ",
                otherConnectionsFromAddress.Select(session => session.Name));

            _chatManager.SendAdminAlert(Loc.GetString("admin-alert-shared-connection",
                ("player", username),
                ("otherCount", otherConnectionCount),
                ("otherList", otherUsernames)));
        }

        /*
         * TODO: Jesus H Christ what is this utter mess of a function
         * TODO: Break this apart into is constituent steps.
         */
        private async Task<(ConnectionDenyReason, string, List<ServerBanDef>? bansHit)?> ShouldDeny(
            NetConnectingArgs e, IPAddress addr) // Starlight: accept resolved IP
        {
            // Check if banned.
            var userId = e.UserId;
            ImmutableArray<byte>? hwId = e.UserData.HWId;
            if (hwId.Value.Length == 0 || !_cfg.GetCVar(CCVars.BanHardwareIds))
            {
                // HWId not available for user's platform, don't look it up.
                // Or hardware ID checks disabled.
                hwId = null;
            }

            var modernHwid = e.UserData.ModernHWIds;

            if (modernHwid.Length == 0 && e.AuthType == LoginType.LoggedIn && _cfg.GetCVar(CCVars.RequireModernHardwareId))
            {
                return (ConnectionDenyReason.NoHwid, Loc.GetString("hwid-required"), null);
            }
            var bans = await _banManager.GetServerBansAsync(addr, userId, hwId, modernHwid, includeUnbanned: false); // NullLink-edit: move to general method at Manager
            if (bans.Count > 0)
            {
                var firstBan = bans[0];
                var message = firstBan.FormatBanMessage(_cfg, _loc);
                return (ConnectionDenyReason.Ban, message, bans);
            }

            if (HasTemporaryBypass(userId))
            {
                _sawmill.Verbose("User {UserId} has temporary bypass, skipping further connection checks", userId);
                return null;
            }

            var adminData = await _db.GetAdminDataForAsync(e.UserId);

            if (_cfg.GetCVar(CCVars.PanicBunkerEnabled) && adminData == null)
            {
                var showReason = _cfg.GetCVar(CCVars.PanicBunkerShowReason);
                var customReason = _cfg.GetCVar(CCVars.PanicBunkerCustomReason);

                var minMinutesAge = _cfg.GetCVar(CCVars.PanicBunkerMinAccountAge);
                var record = await _db.GetPlayerRecordByUserId(userId);
                var bypassAllowed = _cfg.GetCVar(CCVars.BypassBunkerWhitelist) && await _db.GetWhitelistStatusAsync(userId);

                // NullLink
                try
                {
                    if (!bypassAllowed
                        && _bunkerBypass is not null
                        && _actors.TryGetServerGrain(out var serverGrain))
                        bypassAllowed = await serverGrain.HasPlayerAnyRole(userId, _bunkerBypass.Roles);
                }
                catch (Exception)
                {
                }

                var minOverallMinutes = _cfg.GetCVar(CCVars.PanicBunkerMinOverallMinutes);
                var overallTime = (await _db.GetPlayTimes(e.UserId)).Find(p => p.Tracker == PlayTimeTrackingShared.TrackerOverall) ?? new();

                try
                {
                    if (_actors.TryGetServerGrain(out var serverGrain)
                        && _prototypeManager.TryIndex<ServerPlaytimeRecognitionPrototype>(_project ?? "", out var proto)
                        && proto.Recognition.TryGetValue(_server ?? "", out var recs))
                    {
                        var nulllinkPlaytime = await serverGrain.GetPlayTime(e.UserId, [PlayTimeTrackingShared.TrackerOverall], [.. recs]);
                        overallTime.TimeSpent += TimeSpan.FromSeconds(nulllinkPlaytime.Sum(x => x.Time.TotalSeconds));
                    }
                }
                catch (Exception)
                {
                }

                var validAccountAge = record != null &&
                      record.FirstSeenTime.CompareTo(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(minMinutesAge)) <= 0;
                // NullLink

                // Use the custom reason if it exists & they don't have the minimum account age
                if (customReason != string.Empty && !validAccountAge && !bypassAllowed)
                {
                    return (ConnectionDenyReason.Panic, customReason, null);
                }

                if (showReason && !validAccountAge && !bypassAllowed)
                {
                    return (ConnectionDenyReason.Panic,
                        Loc.GetString("panic-bunker-account-denied-reason",
                            ("reason", Loc.GetString("panic-bunker-account-reason-account", ("minutes", minMinutesAge)))), null);
                }

                var haveMinOverallTime = overallTime != null && overallTime.TimeSpent.TotalMinutes > minOverallMinutes;

                // Use the custom reason if it exists & they don't have the minimum time
                if (customReason != string.Empty && !haveMinOverallTime && !bypassAllowed)
                {
                    return (ConnectionDenyReason.Panic, customReason, null);
                }

                if (showReason && !haveMinOverallTime && !bypassAllowed)
                {
                    return (ConnectionDenyReason.Panic,
                        Loc.GetString("panic-bunker-account-denied-reason",
                            ("reason", Loc.GetString("panic-bunker-account-reason-overall", ("minutes", minOverallMinutes)))), null);
                }

                if ((!validAccountAge || !haveMinOverallTime) && !bypassAllowed) // starlight
                {
                    return (ConnectionDenyReason.Panic, Loc.GetString("panic-bunker-account-denied"), null);
                }
            }

            _ticker ??= _entityManager.SystemOrNull<GameTicker>();
            var wasInGame = _ticker != null &&
                            _ticker.PlayerGameStatuses.TryGetValue(userId, out var status) &&
                            status == PlayerGameStatus.JoinedGame;
            var adminBypass = _cfg.GetCVar(CCVars.AdminBypassMaxPlayers) && adminData != null;
            var softPlayerCount = _plyMgr.PlayerCount;

            if (!_cfg.GetCVar(CCVars.AdminsCountForMaxPlayers))
            {
                softPlayerCount -= _adminManager.ActiveAdmins.Count();
            }

            if ((softPlayerCount >= _cfg.GetCVar(CCVars.SoftMaxPlayers) && !adminBypass) && !wasInGame)
            {
                return (ConnectionDenyReason.Full, Loc.GetString("soft-player-cap-full"), null);
            }

            // Checks for whitelist IF it's enabled AND the user isn't an admin. Admins are always allowed.
            if (_cfg.GetCVar(CCVars.WhitelistEnabled) && adminData is null)
            {
                if (_whitelists is null)
                {
                    _sawmill.Error("Whitelist enabled but no whitelists loaded.");
                    // Misconfigured, deny everyone.
                    return (ConnectionDenyReason.Whitelist, Loc.GetString("generic-misconfigured"), null);
                }

                foreach (var whitelist in _whitelists)
                {
                    if (!IsValid(whitelist, softPlayerCount))
                    {
                        // Not valid for current player count.
                        continue;
                    }

                    var whitelistStatus = await IsWhitelisted(whitelist, e.UserData, _sawmill);
                    if (!whitelistStatus.isWhitelisted)
                    {
                        // Not whitelisted.
                        return (ConnectionDenyReason.Whitelist, Loc.GetString("whitelist-fail-prefix", ("msg", whitelistStatus.denyMessage!)), null);
                    }

                    // Whitelisted, don't check any more.
                    break;
                }
            }

            // ALWAYS keep this at the end, to preserve the API limit.
            if (_cfg.GetCVar(CCVars.GameIPIntelEnabled) && adminData == null)
            {
                var result = await _ipintel.IsVpnOrProxy(e, addr); // Starlight: pass resolved IP

                if (result.IsBad)
                    return (ConnectionDenyReason.IPChecks, result.Reason, null);
            }

            return null;
        }

        private bool HasTemporaryBypass(NetUserId user)
        {
            return _temporaryBypasses.TryGetValue(user, out var time) && time > _gameTiming.RealTime;
        }

        private async Task<NetUserId?> AssignUserIdCallback(string name)
        {
            if (!_cfg.GetCVar(CCVars.GamePersistGuests))
            {
                return null;
            }

            var userId = await _db.GetAssignedUserIdAsync(name);
            if (userId != null)
            {
                return userId;
            }

            var assigned = new NetUserId(Guid.NewGuid());
            await _db.AssignUserIdAsync(name, assigned);
            return assigned;
        }
    }
}
