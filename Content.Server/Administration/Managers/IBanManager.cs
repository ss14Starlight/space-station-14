using System.Collections.Immutable;
using System.Net;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.Database;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Managers;

public interface IBanManager
{
    public void Initialize();
    public void Restart();

    /// <summary>
    /// Bans the specified target, address range and / or HWID. One of them must be non-null
    /// </summary>
    /// <param name="target">Target user, username or GUID, null for none</param>
    /// <param name="banningAdmin">The person who banned our target</param>
    /// <param name="addressRange">Address range, null for none</param>
    /// <param name="hwid">H</param>
    /// <param name="minutes">Number of minutes to ban for. 0 and null mean permanent</param>
    /// <param name="severity">Severity of the resulting ban note</param>
    /// <param name="reason">Reason for the ban</param>
    public void CreateServerBan(NetUserId? target, string? targetUsername, NetUserId? banningAdmin, (IPAddress, int)? addressRange, ImmutableTypedHwid? hwid, uint? minutes, NoteSeverity severity, string reason);

    /// <summary>
    /// Gets a list of prefixed prototype IDs with the player's role bans.
    /// </summary>
    public HashSet<string>? GetRoleBans(NetUserId playerUserId);

    /// <summary>
    /// Checks if the player is currently banned from any of the listed roles.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="antags">A list of valid antag prototype IDs.</param>
    /// <returns>Returns True if an active role ban is found for this player for any of the listed roles.</returns>
    public bool IsRoleBanned(ICommonSession player, List<ProtoId<AntagPrototype>> antags);

    /// <summary>
    /// Checks if the player is currently banned from any of the listed roles.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="jobs">A list of valid job prototype IDs.</param>
    /// <returns>Returns True if an active role ban is found for this player for any of the listed roles.</returns>
    public bool IsRoleBanned(ICommonSession player, List<ProtoId<JobPrototype>> jobs);

    /// <summary>
    /// Gets a list of prototype IDs with the player's job bans.
    /// </summary>
    public HashSet<ProtoId<JobPrototype>>? GetJobBans(NetUserId playerUserId);

    /// <summary>
    /// Gets a list of prototype IDs with the player's antag bans.
    /// </summary>
    public HashSet<ProtoId<AntagPrototype>>? GetAntagBans(NetUserId playerUserId);

    /// <summary>
    /// Creates a job ban for the specified target, username or GUID
    /// </summary>
    /// <param name="target">Target user, username or GUID, null for none</param>
    /// <param name="targetUsername">The username of the target, if known</param>
    /// <param name="banningAdmin">The responsible admin for the ban</param>
    /// <param name="addressRange">The range of IPs that are to be banned, if known</param>
    /// <param name="hwid">The HWID to be banned, if known</param>
    /// <param name="role">The role ID to be banned from. Either an AntagPrototype or a JobPrototype</param>
    /// <param name="minutes">Number of minutes to ban for. 0 and null mean permanent</param>
    /// <param name="severity">Severity of the resulting ban note</param>
    /// <param name="reason">Reason for the ban</param>
    /// <param name="timeOfBan">Time when the ban was applied, used for grouping role bans</param>
    public void CreateRoleBan<T>(
        NetUserId? target,
        string? targetUsername,
        NetUserId? banningAdmin,
        (IPAddress, int)? addressRange,
        ImmutableTypedHwid? hwid,
        ProtoId<T> role,
        uint? minutes,
        NoteSeverity severity,
        string reason,
        DateTimeOffset timeOfBan
    ) where T : class, IPrototype;

    // Starlight start
    /// <summary>
    /// Posts a webhook about a (potentially multi-)role ban, e.g. to update Discord
    /// </summary>
    /// <param name="target">Target user, username or GUID, null for none</param>
    /// <param name="targetUsername">The username of the target, if known</param>
    /// <param name="banningAdmin">The responsible admin for the ban</param>
    /// <param name="addressRange">The range of IPs that are to be banned, if known</param>
    /// <param name="hwid">The HWID to be banned, if known</param>
    /// <param name="roles">The role names to be banned from.</param>
    /// <param name="minutes">Number of minutes to ban for. 0 and null mean permanent</param>
    /// <param name="severity">Severity of the resulting ban note</param>
    /// <param name="reason">Reason for the ban</param>
    /// <param name="timeOfBan">Time when the ban was applied, used for grouping role bans</param>
    public void WebhookUpdateRoleBans(
        NetUserId? target,
        string? targetUsername,
        NetUserId? banningAdmin,
        (IPAddress, int)? addressRange,
        ImmutableTypedHwid? hwid,
        IReadOnlyCollection<string> roles,
        uint? minutes,
        NoteSeverity severity,
        string reason,
        DateTimeOffset timeOfBan);
    // Starlight end

    /// <summary>
    /// Pardons a role ban for the specified target, username or GUID
    /// </summary>
    /// <param name="banId">The id of the role ban to pardon.</param>
    /// <param name="unbanningAdmin">The admin, if any, that pardoned the role ban.</param>
    /// <param name="unbanTime">The time at which this role ban was pardoned.</param>
    public Task<string> PardonRoleBan(int banId, NetUserId? unbanningAdmin, DateTimeOffset unbanTime);

    /// <summary>
    /// Sends role bans to the target
    /// </summary>
    /// <param name="pSession">Player's session</param>
    public void SendRoleBans(ICommonSession pSession);

    #region Starlight
    /// <summary>
    /// Asynchronously retrieves a list of server ban definitions matching the specified criteria.
    /// </summary>
    /// <param name="address">The IP address to filter bans by. Specify null to ignore this criterion.</param>
    /// <param name="userId">The user ID to filter bans by. Specify null to ignore this criterion.</param>
    /// <param name="hwId">The legacy hardware ID to filter bans by. Specify null to ignore this criterion.</param>
    /// <param name="modernHWIds">A collection of modern hardware IDs to filter bans by. Specify null to ignore this criterion.</param>
    /// <param name="includeUnbanned">true to include bans that have been unbanned; otherwise, false.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of server ban definitions
    /// that match the provided filters. The list is empty if no bans are found.</returns>
    public Task<List<ServerBanDef>> GetServerBansAsync(IPAddress? address, NetUserId? userId, ImmutableArray<byte>? hwId, ImmutableArray<ImmutableArray<byte>>? modernHWIds, bool includeUnbanned = true);

    /// <summary>
    /// Creates a record of an unban action for a previously issued server ban.
    /// </summary>
    /// <remarks>Use this method to log or process the removal of a server ban, ensuring auditability and
    /// proper tracking of unban events.</remarks>
    /// <param name="banId">The unique identifier of the ban to be lifted. Must correspond to an existing ban.</param>
    /// <param name="unbanningAdmin">The user ID of the administrator performing the unban action, or null if the unban is automated or not
    /// attributed to a specific user.</param>
    /// <param name="unbanTime">The date and time when the unban takes effect.</param>
    /// <returns>A task that represents the asynchronous operation of recording the unban action.</returns>
    public Task CreateServerUnban(int banId, NetUserId? unbanningAdmin, DateTimeOffset unbanTime);

    /// <summary>
    /// Retrieves the details of a server ban with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the server ban to retrieve.</param>
    /// <param name="project">The optional project name to scope the search. If null, the default project is used.</param>
    /// <param name="server">The optional server name to further filter the search. If null, all servers within the project are considered.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the server ban details if found;
    /// otherwise, null.</returns>
    public Task<ServerBanDef?> GetServerBanAsync(int id, string? project = null, string? server = null);

    /// <summary>
    /// Asynchronously retrieves a server ban record that matches the specified address, user ID, hardware ID, or set of
    /// modern hardware IDs.
    /// </summary>
    /// <remarks>At least one identifying parameter should be provided to perform a meaningful search. The
    /// method checks all provided identifiers and returns the first matching ban, if any.</remarks>
    /// <param name="address">The IP address to search for an associated server ban. Can be null if not searching by address.</param>
    /// <param name="userId">The user ID to search for an associated server ban. Can be null if not searching by user ID.</param>
    /// <param name="hwId">A hardware ID to search for an associated server ban. Can be null if not searching by hardware ID.</param>
    /// <param name="modernHWIds">A collection of modern hardware IDs to search for an associated server ban. Can be null if not searching by
    /// modern hardware IDs.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the matching server ban record if
    /// found; otherwise, null.</returns>
    public Task<ServerBanDef?> GetServerBanAsync(IPAddress? address, NetUserId? userId, ImmutableArray<byte>? hwId, ImmutableArray<ImmutableArray<byte>>? modernHWIds);

    /// <summary>
    /// Asynchronously retrieves a list of server role bans that match the specified criteria.
    /// </summary>
    /// <remarks>Multiple criteria can be combined to narrow the search. If all filter parameters are null,
    /// all server role bans are returned, subject to the value of includeUnbanned.</remarks>
    /// <param name="address">The IP address to filter bans by. Specify null to ignore this criterion.</param>
    /// <param name="userId">The user ID to filter bans by. Specify null to ignore this criterion.</param>
    /// <param name="hwId">The legacy hardware ID to filter bans by. Specify null to ignore this criterion.</param>
    /// <param name="modernHWIds">A collection of modern hardware IDs to filter bans by. Specify null to ignore this criterion.</param>
    /// <param name="includeUnbanned">true to include bans that have been unbanned; otherwise, false.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of server role ban
    /// definitions matching the specified filters. The list is empty if no bans are found.</returns>
    public Task<List<ServerRoleBanDef>> GetServerRoleBansAsync(IPAddress? address, NetUserId? userId, ImmutableArray<byte>? hwId, ImmutableArray<ImmutableArray<byte>>? modernHWIds, bool includeUnbanned = true);
    #endregion
}
