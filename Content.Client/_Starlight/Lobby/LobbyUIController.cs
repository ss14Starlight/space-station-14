using System.Numerics;
using Content.Client._Starlight.UserInterface;
using Content.Shared._Starlight.Player;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Preferences;
using Robust.Client.UserInterface;

// ReSharper disable CheckNamespace
namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    [Dependency] private ILogManager _log = null!;

    private void HandleOpenPlayerCharacterSetup(MsgOpenPlayerCharacterSetup message) => OpenCharacterSetupWindow(message);

    /// Set the character setup and profile editor to null to completely reload the UI
    private void ReloadGui(MsgOpenPlayerCharacterSetup? playerData = null)
    {
        _characterSetup?.Orphan();
        _characterSetup = null;
        _profileEditor?.Orphan();
        _profileEditor = null;
        ReloadCharacterSetup(playerData);
    }

    /// <summary>
    /// Opens the character editor in its own window, we reuse the one from the lobby for the sake of simplicity.
    /// </summary>
    public void OpenCharacterSetupWindow(MsgOpenPlayerCharacterSetup? playerData = null)
    {
        if (_stateManager.CurrentState is LobbyState { Lobby.CharacterSetupState: { } control } &&
            _characterSetup?.Parent == control)
        {
            _log.GetSawmill(nameof(LobbyUIController))
                .Error(
                    "Cannot open character setup window for another player while currently editing your own character.");
            return;
        }

        // close and reopen
        if (_characterSetupWindow is { IsOpen: true } || _characterSetupPoppedOut)
        {
            _characterSetupWindow?.DisposePopOut();
            _characterSetupWindow?.Close();
            _characterSetupWindow?.Orphan();
            _characterSetupWindow = null;
            _characterSetup?.Orphan();
            _characterSetup = null;
        }

        if (playerData is not null)
            ReloadGui(playerData);

        var (characterGui, _) = EnsureGui();

        // reload these, the lobby button does this so we do it aswell
        characterGui.ReloadCharacterPickers();
        _profileEditor?.ResetToDefault();
        _jobPriorityEditor?.LoadJobPriorities();

        // detach the gui from it's parent (Most of the time the lobby)
        characterGui.Orphan();

        var window = new CharacterSetupWindow(characterGui) // Starlight: pass the borrowed gui so it can be popped out
        {
            Title = Loc.GetString("ghost-gui-character-editor-button"),
        };
        window.MinSize = window.SetSize = new Vector2(1400, 700); // Might need adjusting but felt good to me

        window.Contents.AddChild(characterGui);

        // when the window closes, detach the gui from it so the gui isn't cleaned up with the window.
        window.OnFinalClose += () =>
        {
            // Rescue the borrowed gui from being disposed with the window
            var lobbyContainer = (_stateManager.CurrentState as LobbyState)?.Lobby?.CharacterSetupState;
            if (characterGui.Parent != lobbyContainer)
                characterGui.Orphan();

            _characterSetupWindow = null;
            _characterSetupPoppedOut = false;
        };

        if (playerData is not null)
            window.OnFinalClose += () => ReloadGui();

        // Starlight: once popped out the in-game window is gone, but keep the window ref so
        // we can still teardown the window.
        window.OnPopout += () => _characterSetupPoppedOut = true;

        _characterSetupWindow = window;
        window.OpenCentered();
    }

    // Starlight: Reworked to PopOutWindow for popout support
    private sealed class CharacterSetupWindow : PopOutWindow
    {
        protected override Control Control { get; } // Starlight: content that moves into the desktop window on popout

        public CharacterSetupWindow(Control content) // Starlight: take the content to pop out
        {
            Control = content; // Starlight
            CloseButton.Visible = false;
        }
    }
}
