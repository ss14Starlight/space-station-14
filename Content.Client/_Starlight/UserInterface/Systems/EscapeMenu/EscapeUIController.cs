// ReSharper disable CheckNamespace
namespace Content.Client.UserInterface.Systems.EscapeMenu;

public sealed partial class EscapeUIController
{
    private void UpdatePendingPreferencesButton() => _escapeWindow?.PendingPreferencesButton.Visible =
        _preferences.PendingPreferences is not null;
}
