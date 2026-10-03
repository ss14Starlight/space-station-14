using System.Threading;
using System.Threading.Tasks;
using Content.Server._Starlight.Administration.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Preferences;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

// ReSharper disable CheckNamespace
namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    [Dependency] private IChatManager _chat = null!;
    [Dependency] private IAdminManager _admin = null!;
    [Dependency] private IAdminLogManager _aLog = null!;

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

    private async void HandleForceUpdatePlayerCharacter(MsgForceUpdatePlayerCharacter message)
    {
        try
        {
            if (!_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session)) return;
            if (!TryEnsureAdminForPrefsUpdate(session, message.UserId)) return;

            await SetProfile(message.UserId, message.Slot, message.Profile);

            // Return if not online
            if (!_playerManager.TryGetSessionById(message.UserId, out var target))
                return;

            ForceUpdateClientPrefs(target, GetPreferences(target.UserId), message.NotifyPlayer);
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

            await DeleteProfile(message.UserId, message.Slot);

            // Return if not online
            if (!_playerManager.TryGetSessionById(message.UserId, out var target))
                return;

            ForceUpdateClientPrefs(target, GetPreferences(target.UserId), message.NotifyPlayer);
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

            var curPrefs = GetPreferences(message.UserId);

            if (!curPrefs.Characters.TryGetValue(message.CharacterIndex, out var characterProfile))
                return;

            // This doesn't NEED to be here but im leaving it because potentially new profile type soon thanks to BarkingPlatypus.
            if (characterProfile is not HumanoidCharacterProfile profile)
                return;

            profile.Enabled = message.EnabledValue;
            var profiles = new Dictionary<int, HumanoidCharacterProfile>(curPrefs.Characters)
            {
                [message.CharacterIndex] = new(profile),
            };

            curPrefs = new PlayerPreferences(profiles, curPrefs.AdminOOCColor, curPrefs.ConstructionFavorites, curPrefs.JobPriorities);
            _cachedPlayerPrefs[message.UserId] = new PlayerPrefData { Prefs = curPrefs, PrefsLoaded = true };

            if (!_playerManager.TryGetSessionById(message.UserId, out var target))
            {
                await _db.SaveCharacterSlotAsync(message.UserId, profile, message.CharacterIndex);
                return;
            }

            if (ShouldStorePrefs(target.Channel.AuthType))
                await _db.SaveCharacterSlotAsync(message.UserId, profile, message.CharacterIndex);

            ForceUpdateClientPrefs(target, curPrefs, message.NotifyPlayer);
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

            await SetJobPriorities(message.UserId, message.JobPriorities);

            // Return if not online
            if (!_playerManager.TryGetSessionById(message.UserId, out var target))
                return;

            ForceUpdateClientPrefs(target, GetPreferences(target.UserId), message.NotifyPlayer);
        }
        catch (Exception e)
        {
            _sawmill.Log(LogLevel.Error, e, $"Failed to update profile for player {message.UserId}.");
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
        if (!_admin.IsAdmin(session))
        {
            _sawmill.Warning(
                $"{session} attempted to force refresh prefs for {target.UserId} and was not admin.");
            _aLog.Add(LogType.AdminCommands, LogImpact.Extreme,
                $"{session} attempted to force refresh prefs for {target.UserId} and was not admin.");
            return false;
        }

        var message = $"{session} has updated character preferences for {target.UserId}.";
        _aLog.Add(LogType.AdminCommands, LogImpact.High, $"{message}");
        _chat.SendAdminAnnouncement($"{message}");
        IoCManager.Resolve<AutoDiscordLogSystem>().LogToDiscord(message); // Yes this needs to be resolved here, IDK why.
        return true;
    }
}
