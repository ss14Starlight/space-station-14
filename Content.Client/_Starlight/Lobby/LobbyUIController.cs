using System.Numerics;
using Content.Client._Starlight.Lobby.UI;
using Content.Client._Starlight.UserInterface;
using Content.Client.Lobby.UI;
using Content.Shared._Starlight.Player;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface;

// ReSharper disable CheckNamespace
namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    [Dependency] private ILogManager _log = null!;

    private PendingPreferencesWindow? _pendingWindow;
    private PendingPreferencesRejectWindow? _pendingRejectWindow;
    private bool _characterSetupWindowEditingOwn;
    private bool _regenerateOnPreferencesLoaded;

    private void HandleOpenPlayerCharacterSetup(MsgOpenPlayerCharacterSetup message)
    {
        if (_preferencesManager.ReviewingPending)
            CloseProfileEditor();
        StopPendingReview();

        if (IsEditingOwnCharacter())
        {
            _log.GetSawmill(nameof(LobbyUIController))
                .Error(
                    "Cannot open character setup window for another player while currently editing your own character.");
            return;
        }

        _preferencesManager.TargetPreferences = message.Preferences;
        _preferencesManager.TargetPlayerInfo = message.PlayerInfo;
        OpenCharacterSetupWindow();
    }

    /// <summary>
    /// Set the character setup and profile editor to null to completely reload the UI
    /// </summary>
    private void ReloadGui()
    {
        _characterSetup?.Orphan();
        _characterSetup = null;
        _profileEditor?.Orphan();
        _profileEditor = null;
        ReloadCharacterSetup();
        _jobPrioritiesWindow?.Refresh(); // Don't let a stale snapshot get saved onto a target player or pending review
    }

    /// <summary>
    /// Opens the character editor in its own window, we reuse the one from the lobby for the sake of simplicity.
    /// </summary>
    public void OpenCharacterSetupWindow()
    {
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

        if (_preferencesManager.TargetPreferences is not null || _preferencesManager.ReviewingPending)
            ReloadGui(); // Reload if now editing target or reviewing

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

            if (_characterSetupWindow != window)
                return;

            _characterSetupWindow = null;
            _characterSetupPoppedOut = false;
        };

        if (_preferencesManager.TargetPreferences is not null)
            // Closing UI, remove references to target player so next usage of the window is back to editing yourself
            window.OnFinalClose += () =>
            {
                _preferencesManager.TargetPreferences = null;
                _preferencesManager.TargetPlayerInfo = null;
                ReloadGui();
            };

        if (_preferencesManager.ReviewingPending)
            window.OnFinalClose += StopPendingReview;

        // Starlight: once popped out the in-game window is gone, but keep the window ref so
        // we can still teardown the window.
        window.OnPopout += () => _characterSetupPoppedOut = true;

        _characterSetupWindow = window;
        _characterSetupWindowEditingOwn =
            _preferencesManager.TargetPreferences is null && !_preferencesManager.ReviewingPending;
        window.OpenCentered();
    }

    /// <summary>
    /// Sets the job priorities and updates all relevant UI elements. This is called when job priorities are changed in the job priorities window or other sources.
    /// </summary>
    public void SetJobPriorities(Dictionary<ProtoId<JobPrototype>, JobPriority> priorities, Control source)
    {
        // Pending changes are read-only, UpdateJobPriorities would drop it anyway. Refresh to undo the edit in the UI.
        if (!_preferencesManager.ReviewingPending)
        {
            _preferencesManager.UpdateJobPriorities(priorities);
            OnAnyCharacterOrJobChange?.Invoke();
        }

        _jobPriorityEditor?.LoadJobPriorities();
        PreviewPanel?.Refresh();

        if (source != _jobPrioritiesWindow || _preferencesManager.ReviewingPending)
            _jobPrioritiesWindow?.Refresh();

        var columns = (_stateManager.CurrentState as LobbyState)?.Lobby?.JobColumns;
        if (source != columns || _preferencesManager.ReviewingPending)
            columns?.Refresh();
    }

    /// <summary>
    /// Opens the job priorities window, which allows for editing job priorities in a more detailed way than the lobby columns.
    /// </summary>
    public void OpenJobPrioritiesWindow()
    {
        if (_jobPrioritiesWindow is { Disposed: false })
        {
            _jobPrioritiesWindow.MoveToFront();
            return;
        }

        _jobPrioritiesWindow = new JobPrioritiesWindow();
        var window = _jobPrioritiesWindow;
        window.OnPrioritiesChanged += priorities => SetJobPriorities(priorities, window);
        _jobPrioritiesWindow.OnClose += () => _jobPrioritiesWindow = null;
        _jobPrioritiesWindow.OpenCentered();
    }

    private bool IsEditingOwnCharacter()
    {
        if (_stateManager.CurrentState is LobbyState { Lobby.CharacterSetupState: { Visible: true } control } &&
            _characterSetup?.Parent == control)
            return true;

        return _characterSetupWindowEditingOwn &&
            (_characterSetupWindow is { IsOpen: true } || _characterSetupPoppedOut);
    }

    private void OnPendingPreferencesReceived()
    {
        if (_preferencesManager.ReviewingPending)
            RefreshPendingReview();
        else
            OpenPendingPreferencesWindow();
    }

    private void OpenPendingPreferencesWindow()
    {
        if (_preferencesManager.PendingPreferences is null)
            return;

        if (_pendingWindow is null)
        {
            _pendingWindow = new PendingPreferencesWindow();
            _pendingWindow.ReviewButton.OnPressed += _ => StartPendingReview();
            _pendingWindow.AcceptButton.OnPressed += _ => AcceptPendingPreferences();
            _pendingWindow.RejectButton.OnPressed += _ => OpenPendingRejectWindow();
            _pendingWindow.OnClose += () => _pendingWindow = null;
        }

        _pendingWindow.ReviewButton.Disabled = _preferencesManager.ReviewingPending;

        _pendingWindow.SetAdmin(_preferencesManager.PendingPreferencesAdmin?.Username ?? string.Empty);
        if (_pendingWindow.IsOpen)
            _pendingWindow.MoveToFront();
        else
            _pendingWindow.OpenCentered();
    }

    public void StartPendingReview()
    {
        if (_preferencesManager.ReviewingPending)
            return;

        _pendingWindow?.Close();
        _preferencesManager.StartPendingReview();

        if (_stateManager.CurrentState is LobbyState lobby)
        {
            _characterSetupWindow?.DisposePopOut();
            _characterSetupWindow?.Close();
            ReloadGui();
            lobby.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
            return;
        }

        OpenCharacterSetupWindow();
    }

    private void StopPendingReview()
    {
        if (!_preferencesManager.ReviewingPending)
            return;

        _preferencesManager.EndPendingReview();
        _profileEditor?.LoadoutWindow?.Close();
        ReloadGui();
    }

    private void TogglePendingView()
    {
        _preferencesManager.ViewingChanges = !_preferencesManager.ViewingChanges;
        RefreshPendingReview();
    }

    private void RefreshPendingReview()
    {
        if (_characterSetup is null || _profileEditor is null)
            return;

        if (EditedSlot is { } slot && _preferencesManager.Preferences.Characters.ContainsKey(slot))
            _profileEditor.SetProfile(slot);
        else
        {
            _profileEditor.SetProfile(null, null);
            _characterSetup.SelectedCharacterSlot = null;
        }

        _jobPriorityEditor?.LoadJobPriorities();
        _characterSetup.ReloadCharacterPickers(selectJobPriorities: _characterSetup.JobPriorityEditor.Visible);
    }

    private void AcceptPendingPreferences()
    {
        _regenerateOnPreferencesLoaded = true;
        ResolvePendingPreferences(_preferencesManager.AcceptPendingPreferences);
    }

    private bool TryRegenerateAfterAccept()
    {
        if (!_regenerateOnPreferencesLoaded)
            return false;

        _regenerateOnPreferencesLoaded = false;
        OnAnyCharacterOrJobChange?.Invoke();

        if (_characterSetupWindow is { IsOpen: true } || _characterSetupPoppedOut)
        {
            OpenCharacterSetupWindow();
            return true;
        }

        _characterSetup?.SelectedCharacterSlot = null;
        ReloadGui();
        return true;
    }

    private void OpenPendingRejectWindow()
    {
        _pendingRejectWindow?.Close();
        _pendingRejectWindow = new PendingPreferencesRejectWindow();
        _pendingRejectWindow.OnSubmit += reason =>
            ResolvePendingPreferences(() => _preferencesManager.RejectPendingPreferences(reason));
        _pendingRejectWindow.OnClose += () => _pendingRejectWindow = null;
        _pendingRejectWindow.OpenCentered();
    }

    private void ResolvePendingPreferences(Action resolve)
    {
        var reviewing = _preferencesManager.ReviewingPending;
        resolve();
        _pendingWindow?.Close();

        if (!reviewing)
            return;

        _profileEditor?.LoadoutWindow?.Close();
        CloseProfileEditor();
        ReloadGui();
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
