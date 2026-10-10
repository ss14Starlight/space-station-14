using System.Linq;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

// ReSharper disable CheckNamespace
namespace Content.Client.Lobby;

public sealed partial class ClientPreferencesManager
{
    public void SetCharacterEnableForPrefs(int slot, MsgOpenPlayerCharacterSetup playerData, bool enable = true)
    {
        if (!playerData.Preferences.Characters.TryGetValue(slot, out var characterProfile))
            return;

        var characters = new Dictionary<int, HumanoidCharacterProfile>(playerData.Preferences.Characters)
        {
            [slot] = new(characterProfile) { Enabled = enable },
        };
        playerData.Preferences.SetCharacters(characters);

        var msg = new MsgForcePlayerCharacterEnable
        {
            CharacterIndex = slot, EnabledValue = enable, UserId = playerData.PlayerInfo.UserId, NotifyPlayer = true
        };
        _netManager.ClientSendMessage(msg);
    }

    public void UpdateCharacterForPrefs(HumanoidCharacterProfile profile, MsgOpenPlayerCharacterSetup playerData,
        int slot)
    {
        var collection = IoCManager.Instance!;
        profile.EnsureValid(_playerManager.LocalSession!, collection);
        var characters =
            new Dictionary<int, HumanoidCharacterProfile>(playerData.Preferences.Characters) { [slot] = profile };
        playerData.Preferences.SetCharacters(characters);
        var msg = new MsgForceUpdatePlayerCharacter
        {
            Profile = profile, Slot = slot, UserId = playerData.PlayerInfo.UserId, NotifyPlayer = true
        };
        _netManager.ClientSendMessage(msg);
    }

    public void CreateCharacterForPrefs(HumanoidCharacterProfile profile, MsgOpenPlayerCharacterSetup playerData)
    {
        var characters = new Dictionary<int, HumanoidCharacterProfile>(playerData.Preferences.Characters);
        var lowest = Enumerable.Range(0, Settings.MaxCharacterSlots) // Note: may need updating later down the line
            .Except(characters.Keys)
            .FirstOrNull();

        if (lowest == null)
        {
            throw new InvalidOperationException("Out of character slots!");
        }

        var l = lowest.Value;
        characters.Add(l, profile);
        playerData.Preferences.SetCharacters(characters);

        UpdateCharacterForPrefs(profile, playerData, l);
    }

    public void DeleteCharacterForPrefs(HumanoidCharacterProfile profile, MsgOpenPlayerCharacterSetup playerData) =>
        DeleteCharacter(playerData.Preferences.IndexOfCharacter(profile));

    public void DeleteCharacterForPrefs(int slot, MsgOpenPlayerCharacterSetup playerData)
    {
        var characters = playerData.Preferences.Characters.Where(p => p.Key != slot);
        playerData.Preferences.SetCharacters(characters);
        var msg = new MsgForceDeletePlayerCharacter
        {
            Slot = slot, UserId = playerData.PlayerInfo.UserId, NotifyPlayer = true
        };
        _netManager.ClientSendMessage(msg);
    }

    public void UpdateJobPrioritiesForPrefs(Dictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities,
        MsgOpenPlayerCharacterSetup playerData)
    {
        playerData.Preferences.JobPriorities = jobPriorities;
        var msg = new MsgForceUpdatePlayerJobPriorities
        {
            JobPriorities = jobPriorities, UserId = playerData.PlayerInfo.UserId, NotifyPlayer = true
        };
        _netManager.ClientSendMessage(msg);
    }
}
