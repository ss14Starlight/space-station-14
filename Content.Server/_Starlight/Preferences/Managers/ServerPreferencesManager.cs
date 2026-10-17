using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Starlight.Administration.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Humanoid;
using Content.Shared._Starlight.Player;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

// ReSharper disable CheckNamespace
namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    [Dependency] private IChatManager _chat = null!;
    [Dependency] private IAdminManager _admin = null!;
    [Dependency] private IAdminLogManager _aLog = null!;
    [Dependency] private IEntitySystemManager _entSys = null!;
    [Dependency] private ISerializationManager _serialization = null!;

    private const int MaxRejectionMessageLength = 512;

    public async Task<PlayerPreferences?> GetProfileDataForPlayerAsync(NetUserId targetPlayer,
        CancellationToken cancel)
    {
        PlayerPrefData? data = null;
        {
            if (_cachedPlayerPrefs.TryGetValue(targetPlayer, out var prefs))
                data = prefs;
        }
        if (data is not null) return data.Prefs;

        {
            var prefs = await GetOrCreatePreferencesAsync(targetPlayer, cancel);
            data = new PlayerPrefData { Prefs = prefs, PrefsLoaded = true }; // Flag it loaded so it can be edited
            _cachedPlayerPrefs[targetPlayer] = data;
        }

        return data.Prefs;
    }

    /// <summary>
    /// Gets the preferences for a player with any pending admin edits applied on top.
    /// </summary>
    public async Task<PlayerPreferences?> GetProfileDataWithPendingAsync(NetUserId targetPlayer,
        CancellationToken cancel)
    {
        var prefs = await GetProfileDataForPlayerAsync(targetPlayer, cancel);
        if (prefs is null)
            return null;

        var pending = await _db.GetPendingPreferenceAsync(targetPlayer, cancel);
        return pending is null ? prefs : ApplyPending(prefs, pending);
    }

    private async void HandleForceUpdatePlayerCharacter(MsgForceUpdatePlayerCharacter message)
    {
        try
        {
            if (!_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session)) return;
            if (!TryEnsureAdminForPrefsUpdate(session, message.UserId)) return;
            if (message.Slot < 0 || message.Slot >= MaxCharacterSlots) return;

            await _db.SavePendingProfileAsync(message.UserId, session.UserId, session.Name, message.Slot,
                SerializeProfile(message.Profile));
            await SendPendingPreferencesAsync(message.UserId, message.NotifyPlayer);
        }
        catch (Exception e)
        {
            _sawmill.Log(LogLevel.Error, e, $"Failed to update profile for player {message.UserId}.");
        }
    }

    private async void HandleForceDeletePlayerCharacter(MsgForceDeletePlayerCharacter message)
    {
        try
        {
            if (!_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session)) return;
            if (!TryEnsureAdminForPrefsUpdate(session, message.UserId)) return;
            if (message.Slot < 0 || message.Slot >= MaxCharacterSlots) return;

            await _db.SavePendingProfileAsync(message.UserId, session.UserId, session.Name, message.Slot, null);
            await SendPendingPreferencesAsync(message.UserId, message.NotifyPlayer);
        }
        catch (Exception e)
        {
            _sawmill.Log(LogLevel.Error, e, $"Failed to update profile for player {message.UserId}.");
        }
    }

    private async void HandleForcePlayerCharacterEnable(MsgForcePlayerCharacterEnable message)
    {
        try
        {
            if (!_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session)) return;
            if (!TryEnsureAdminForPrefsUpdate(session, message.UserId)) return;

            // Toggle on top of any pending edit to this slot, not the saved profile.
            var curPrefs = await GetProfileDataWithPendingAsync(message.UserId, CancellationToken.None);

            if (curPrefs is null || !curPrefs.Characters.TryGetValue(message.CharacterIndex, out var characterProfile))
                return;

            var profile = new HumanoidCharacterProfile(characterProfile) { Enabled = message.EnabledValue };

            await _db.SavePendingProfileAsync(message.UserId, session.UserId, session.Name, message.CharacterIndex,
                SerializeProfile(profile));
            await SendPendingPreferencesAsync(message.UserId, message.NotifyPlayer);
        }
        catch (Exception e)
        {
            _sawmill.Log(LogLevel.Error, e, $"Failed to update profile for player {message.UserId}.");
        }
    }

    private async void HandleForceUpdatePlayerJobPriorities(MsgForceUpdatePlayerJobPriorities message)
    {
        try
        {
            if (!_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session)) return;
            if (!TryEnsureAdminForPrefsUpdate(session, message.UserId)) return;

            await _db.SavePendingJobPrioritiesAsync(message.UserId, session.UserId, session.Name,
                message.JobPriorities);
            await SendPendingPreferencesAsync(message.UserId, message.NotifyPlayer);
        }
        catch (Exception e)
        {
            _sawmill.Log(LogLevel.Error, e, $"Failed to update job priorities for player {message.UserId}.");
        }
    }

    private async void HandlePreferenceConfirmationResult(MsgPreferenceConfirmationResult message)
    {
        var userId = message.MsgChannel.UserId;
        try
        {
            if (!_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session)) return;

            var pending = await _db.GetPendingPreferenceAsync(userId, CancellationToken.None);
            if (pending is null)
                return;

            if (pending.Revision != message.Revision)
            {
                // An admin edited the pending data after the player was sent it, have them review it again.
                await SendPendingPreferencesAsync(session, notifyPlayer: true);
                return;
            }

            if (message.Accepted)
            {
                foreach (var pendingProfile in pending.Profiles)
                {
                    if (pendingProfile.Data is null)
                        await DeleteProfile(userId, pendingProfile.Slot);
                    else if (TryDeserializeProfile(pendingProfile.Data, out var profile))
                        await SetProfile(userId, pendingProfile.Slot, profile);
                }

                if (pending.HasJobPriorities)
                    await SetJobPriorities(userId, ConvertPendingJobPriorities(pending));
            }

            if (!await _db.DeletePendingPreferenceAsync(userId, pending.Revision))
            {
                await SendPendingPreferencesAsync(session, notifyPlayer: true);
                return;
            }

            var rejection = message.RejectionMessage is { } reason
                ? $" Reason: {reason[..Math.Min(reason.Length, MaxRejectionMessageLength)]}"
                : string.Empty;
            var logMessage = message.Accepted
                ? $"{session} accepted preference edits made by {pending.LastAdminName} ({pending.LastAdminUserId})."
                : $"{session} rejected preference edits made by {pending.LastAdminName} ({pending.LastAdminUserId}).{rejection}";
            _aLog.Add(LogType.AdminCommands, LogImpact.Medium, $"{logMessage}");
            _chat.SendAdminAnnouncement(logMessage);

            ForceUpdateClientPrefs(session, GetPreferences(userId), message.Accepted);
        }
        catch (Exception e)
        {
            _sawmill.Log(LogLevel.Error, e, $"Failed to resolve pending preferences for player {userId}.");
        }
    }

    private async Task SendPendingPreferencesAsync(NetUserId userId, bool notifyPlayer)
    {
        if (_playerManager.TryGetSessionById(userId, out var session))
            await SendPendingPreferencesAsync(session, notifyPlayer);
    }

    private async Task SendPendingPreferencesAsync(ICommonSession session, bool notifyPlayer = true)
    {
        try
        {
            if (!_cachedPlayerPrefs.TryGetValue(session.UserId, out var prefsData) || !prefsData.PrefsLoaded)
                return;

            var pending = await _db.GetPendingPreferenceAsync(session.UserId, CancellationToken.None);
            if (pending is null)
                return;

            var msg = new MsgHasPendingPreferenceUpdates
            {
                Preferences = ApplyPending(prefsData.Prefs!, pending),
                PlayerInfo = new MinimalPlayerInfo(session.Name, session.UserId),
                LastAdmin = new MinimalPlayerInfo(pending.LastAdminName, new NetUserId(pending.LastAdminUserId)),
                Revision = pending.Revision,
            };
            _netManager.ServerSendMessage(msg, session.Channel);

            if (!notifyPlayer)
                return;
            const string Message = "An admin has proposed changes to your character preferences. Review them to accept or reject.";
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", FormattedMessage.EscapeText(Message)));
            _chat.ChatMessageToOne(ChatChannel.Server, Message, wrappedMessage,
                EntityUid.Invalid, false, session.Channel, Color.Gold);
        }
        catch (Exception e)
        {
            _sawmill.Log(LogLevel.Error, e, $"Failed to send pending preferences to player {session.UserId}.");
        }
    }

    private PlayerPreferences ApplyPending(PlayerPreferences prefs, StarLightModel.PendingPreference pending)
    {
        var characters = new Dictionary<int, HumanoidCharacterProfile>(prefs.Characters);
        foreach (var pendingProfile in pending.Profiles)
        {
            if (pendingProfile.Data is null)
                characters.Remove(pendingProfile.Slot);
            else if (TryDeserializeProfile(pendingProfile.Data, out var profile))
                characters[pendingProfile.Slot] = profile;
        }

        var jobPriorities = pending.HasJobPriorities ? ConvertPendingJobPriorities(pending) : prefs.JobPriorities;
        return new PlayerPreferences(characters, prefs.AdminOOCColor, prefs.ConstructionFavorites, jobPriorities);
    }

    private static Dictionary<ProtoId<JobPrototype>, JobPriority> ConvertPendingJobPriorities(
        StarLightModel.PendingPreference pending) =>
        pending.JobPriorities.ToDictionary(p => new ProtoId<JobPrototype>(p.JobName), p => (JobPriority)p.Priority);

    private string SerializeProfile(HumanoidCharacterProfile profile)
    {
        var node = _entSys.GetEntitySystem<HumanoidAppearanceSystem>().ToDataNode(profile);
        using var writer = new StringWriter();
        node.Write(writer);
        return writer.ToString();
    }

    private bool TryDeserializeProfile(string data, [NotNullWhen(true)] out HumanoidCharacterProfile? profile)
    {
        try
        {
            using var reader = new StringReader(data);
            var yamlStream = new YamlStream();
            yamlStream.Load(reader);
            profile = _serialization.Read<HumanoidProfileExport>(yamlStream.Documents[0].RootNode.ToDataNode(),
                notNullableOverride: true).Profile;
            return true;
        }
        catch (Exception e)
        {
            _sawmill.Log(LogLevel.Error, e, "Failed to deserialize pending profile.");
            profile = null;
            return false;
        }
    }

    private void ForceUpdateClientPrefs(ICommonSession target, PlayerPreferences prefs, bool notifyPlayer)
    {
        var msg = new MsgPreferencesAndSettings
        {
            Preferences = prefs,
            Settings = new GameSettings
            {
                MaxCharacterSlots = MaxCharacterSlots
            }
        };

        _netManager.ServerSendMessage(msg, target.Channel);
        if (!notifyPlayer) return;
        const string Message = "Your character profiles have been updated.";
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", FormattedMessage.EscapeText(Message)));
        _chat.ChatMessageToOne(ChatChannel.Server, Message, wrappedMessage,
            EntityUid.Invalid, false, target.Channel, Color.Gold);
    }

    private bool TryEnsureAdminForPrefsUpdate(ICommonSession session, NetUserId target)
    {
        if (!_admin.HasAdminFlag(session, AdminFlags.Prefs))
        {
            _sawmill.Warning(
                $"{session} attempted to force refresh prefs for {target.UserId} without the Prefs flag.");
            _aLog.Add(LogType.AdminCommands, LogImpact.Extreme,
                $"{session} attempted to force refresh prefs for {target.UserId} without the Prefs flag.");
            return false;
        }

        var message = $"{session} has proposed character preference edits for {target.UserId}.";
        _aLog.Add(LogType.AdminCommands, LogImpact.High, $"{message}");
        _chat.SendAdminAnnouncement($"{message}");
        _entSys.GetEntitySystem<AutoDiscordLogSystem>().LogToDiscord(message);
        return true;
    }
}
