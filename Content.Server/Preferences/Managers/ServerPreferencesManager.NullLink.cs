using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    private void SendPreferences(ICommonSession session, PlayerPreferences prefs)
    {
        var msg = new MsgPreferencesAndSettings
        {
            Preferences = prefs,
            Settings = new GameSettings
            {
                MaxCharacterSlots = MaxCharacterSlots
            }
        };
        _netManager.ServerSendMessage(msg, session.Channel);
    }

    private static HashSet<string> GetSelections(HumanoidCharacterProfile profile)
    {
        var selections = new HashSet<string>();

        foreach (var job in profile.JobPreferences)
            selections.Add($"job:{job}");

        foreach (var antag in profile.AntagPreferences)
            selections.Add($"antag:{antag}");

        foreach (var trait in profile.TraitPreferences)
            selections.Add($"trait:{trait}");

        foreach (var (role, loadout) in profile.Loadouts)
        {
            foreach (var (group, loadouts) in loadout.SelectedLoadouts)
            {
                foreach (var selected in loadouts)
                    selections.Add($"loadout:{role}/{group}/{selected.Prototype}");
            }
        }

        return selections;
    }
}
