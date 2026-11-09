using System.Threading.Tasks;
using Content.Server._Starlight.Administration.Systems;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Preferences;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IEntitySystemManager _entitySystems = default!;

    private const int MaxReportedPrototypeLength = 64;

    // One Discord report per player per server run so a spamming client can't flood the webhook.
    private readonly HashSet<NetUserId> _forcedPrototypeReported = new();

    /// <summary>
    /// Saves a profile sent by a client. If its ForcedPrototype had to be replaced, the client gets the
    /// server's copy back so it stops sending the old value.
    /// </summary>
    private async Task SetClientProfile(NetUserId userId, int slot, HumanoidCharacterProfile profile)
    {
        var checkedProfile = WithServerForcedPrototype(userId, slot, profile);
        await SetProfile(userId, slot, checkedProfile);

        if (ReferenceEquals(checkedProfile, profile))
            return;

        if (_cachedPlayerPrefs.TryGetValue(userId, out var data)
            && data.Prefs is { } prefs
            && _playerManager.TryGetSessionById(userId, out var session))
        {
            SendPreferences(session, prefs);
        }
    }

    /// <summary>
    /// Only admin commands set <see cref="HumanoidCharacterProfile.ForcedPrototype"/>, so a client's save
    /// keeps whatever the server already has for that slot.
    /// </summary>
    private HumanoidCharacterProfile WithServerForcedPrototype(NetUserId userId, int slot, HumanoidCharacterProfile profile)
    {
        var serverValue = string.Empty;
        if (_cachedPlayerPrefs.TryGetValue(userId, out var data)
            && data.Prefs is { } prefs
            && prefs.Characters.TryGetValue(slot, out var current))
        {
            serverValue = current.ForcedPrototype;
        }

        if (profile.ForcedPrototype == serverValue)
            return profile;

        // A normal client sends back the server's value, or nothing for a new or imported character.
        // It can still be stale if it saved before the resend of an admin change arrived.
        if (!string.IsNullOrEmpty(profile.ForcedPrototype) && _playerManager.TryGetSessionById(userId, out var session))
        {
            var attempted = ForReport(profile.ForcedPrototype);

            _adminLog.Add(LogType.Exploit, LogImpact.Extreme,
                $"{session:player} tried to set ForcedPrototype {attempted:forcedPrototype} on character slot {slot:slot}; kept '{serverValue:serverValue}'");

            if (_forcedPrototypeReported.Add(userId))
            {
                _entitySystems.GetEntitySystem<AutoDiscordLogSystem>().LogToDiscord(
                    Loc.GetString("autolog-forcedprototype-attempt",
                        ("prototype", attempted),
                        ("slot", slot),
                        ("kept", serverValue == string.Empty ? Loc.GetString("autolog-forcedprototype-none") : serverValue),
                        ("userId", userId.ToString())),
                    session.Name);
            }
        }

        return profile.WithForcedPrototype(serverValue);
    }

    // The value comes straight from the client: cap it and keep it inside a Discord code span.
    private static string ForReport(string value)
    {
        var shown = value.Length > MaxReportedPrototypeLength ? value[..MaxReportedPrototypeLength] + "..." : value;
        return shown.Replace('`', '\'').ReplaceLineEndings(" ");
    }
}
