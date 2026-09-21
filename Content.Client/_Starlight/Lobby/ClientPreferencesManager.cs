using Content.Shared._Starlight.Player;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Preferences;

// ReSharper disable CheckNamespace
namespace Content.Client.Lobby;

public sealed partial class ClientPreferencesManager
{
    public PlayerPreferences? TargetPreferences { get; set; }
    public PlayerPreferences? PendingPreferences { get; set; }
    public MinimalPlayerInfo? TargetPlayerInfo { get; set; }
    public bool ViewingChanges { get; set; }

    /*
     * TODO:
     * - add button to game menu
     * - add popup when initially receiving the updates
     * - maybe make new scene for editing prefs that way i dont need to worry about lobby editor
     * - autoclose lobby editor if in lobby editor and viewing changes
     */

    private void HandlePendingPreferencesUpdate(MsgHasPendingPreferenceUpdates message)
    {

    }
}
