using Content.Shared._Starlight.Player;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Preferences;

// ReSharper disable CheckNamespace
namespace Content.Client.Lobby;

public sealed partial class ClientPreferencesManager
{
    public PlayerPreferences? TargetPreferences { get; set; }
    public PlayerPreferences? PendingPreferences { get; set; }
    public MinimalPlayerInfo? PendingPreferencesAdmin { get; private set; }
    public MinimalPlayerInfo? TargetPlayerInfo { get; set; }
    public bool ViewingChanges { get; set; }

    public event Action? OnPendingPreferencesReceived;

    private int _pendingRevision;

    /*
     * TODO:
     * - add button to game menu
     * - add popup when initially receiving the updates
     * - maybe make new scene for editing prefs that way i dont need to worry about lobby editor
     * - autoclose lobby editor if in lobby editor and viewing changes
     */

    private void HandlePendingPreferencesUpdate(MsgHasPendingPreferenceUpdates message)
    {
        PendingPreferences = message.Preferences;
        PendingPreferencesAdmin = message.LastAdmin;
        _pendingRevision = message.Revision;
        OnPendingPreferencesReceived?.Invoke();
    }

    public void AcceptPendingPreferences() => SendPendingPreferencesResult(true, null);

    public void RejectPendingPreferences(string? reason = null) => SendPendingPreferencesResult(false, reason);

    private void SendPendingPreferencesResult(bool accepted, string? reason)
    {
        if (PendingPreferences is null)
            return;

        _netManager.ClientSendMessage(new MsgPreferenceConfirmationResult
        {
            Accepted = accepted,
            Revision = _pendingRevision,
            RejectionMessage = reason,
        });

        // The server responds with MsgPreferencesAndSettings once resolved.
        PendingPreferences = null;
        PendingPreferencesAdmin = null;
        ViewingChanges = false;
    }
}
