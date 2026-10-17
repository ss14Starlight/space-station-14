using Content.Client.Guidebook;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._Starlight.Preferences;
using Content.Shared.CCVar;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

#region Starlight
using Content.Client._Starlight.Lobby.UI;
using Content.Client._Starlight.Humanoid;
using Content.Client._Starlight.UserInterface; // Starlight: popout support
using Content.Shared._Starlight.CCVar;
using System.Numerics;
#endregion

namespace Content.Client.Lobby;

public sealed partial class LobbyUIController : UIController, IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    [Dependency] private IClientPreferencesManager _preferencesManager = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private IFileDialogManager _dialogManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private JobRequirementsManager _requirements = default!;
    [Dependency] private MarkingManager _markings = default!;
    [UISystemDependency] private readonly GuidebookSystem _guide = default!;
    [Dependency] private INetManager _net = null!; // Starlight

    private CharacterSetupGui? _characterSetup;
    private HumanoidProfileEditor? _profileEditor;
    private JobPriorityEditor? _jobPriorityEditor;
    private CharacterSetupGuiSavePanel? _savePanel;

    /// begin starlight
    /// <summary>
    /// character editor window, see OpenCharacterSetupWindow()
    /// </summary>
    private PopOutWindow? _characterSetupWindow; // Starlight: PopOutWindow so teardown can close the popped-out desktop window
    private bool _characterSetupPoppedOut; // Starlight: true while the editor lives in a pop out
    //end starlight

    /// <summary>
    /// Event invoked when any character or job selection or job priority is changed.
    /// Basically anything that might change round start character/job selection.
    /// </summary>
    public event Action? OnAnyCharacterOrJobChange;

    /// <summary>
    /// This is the characher preview panel in the chat. This should only update if their character updates.
    /// </summary>
    private LobbyCharacterPreviewPanel? PreviewPanel => GetLobbyPreview();

    /// <summary>
    /// This is the modified profile currently being edited.
    /// </summary>
    private HumanoidCharacterProfile? EditedProfile => _profileEditor?.Profile;

    private int? EditedSlot => _profileEditor?.CharacterSlot;

    public override void Initialize()
    {
        base.Initialize();
        _prototypeManager.PrototypesReloaded += OnProtoReload;
        _preferencesManager.OnServerDataLoaded += PreferencesDataLoaded;
        _requirements.Updated += OnRequirementsUpdated;

        _configurationManager.OnValueChanged(CCVars.FlavorText, args =>
        {
            _profileEditor?.RefreshCharacterInfo(); //starlight
        });

        //begin starlight
        _configurationManager.OnValueChanged(StarlightCCVars.ICSecrets, _ => _profileEditor?.RefreshCharacterInfo());
        _configurationManager.OnValueChanged(StarlightCCVars.OOCNotes, _ => _profileEditor?.RefreshCharacterInfo());
        _configurationManager.OnValueChanged(CCVars.GameRoleTimers, _ => RefreshEditors());
        _configurationManager.OnValueChanged(CCVars.GameRoleLoadoutTimers, _ => RefreshEditors());
        _configurationManager.OnValueChanged(StarlightCCVars.ExploitableSecrets, _ => _profileEditor?.RefreshCharacterInfo());
        //end starlight

        _configurationManager.OnValueChanged(CCVars.GameRoleTimers, _ => RefreshEditors());

        _configurationManager.OnValueChanged(CCVars.GameRoleWhitelist, _ => RefreshEditors());

        _net.RegisterNetMessage<MsgOpenPlayerCharacterSetup>(HandleOpenPlayerCharacterSetup);// Starlight
        _preferencesManager.OnPendingPreferencesReceived += OnPendingPreferencesReceived; // Starlight
    }

    private LobbyCharacterPreviewPanel? GetLobbyPreview()
    {
        if (_stateManager.CurrentState is LobbyState lobby)
        {
            return lobby.Lobby?.CharacterPreview;
        }

        return null;
    }

    private void OnRequirementsUpdated()
    {
        _profileEditor?.RefreshAntags();
        _profileEditor?.RefreshJobs();
        _jobPriorityEditor?.RefreshJobs();
    }

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (_profileEditor != null)
        {
            if (obj.WasModified<AntagPrototype>())
            {
                _profileEditor.RefreshAntags();
            }

            if (obj.WasModified<JobPrototype>() ||
                obj.WasModified<DepartmentPrototype>())
            {
                _profileEditor.RefreshJobs();
            }

            if (obj.WasModified<LoadoutPrototype>() ||
                obj.WasModified<LoadoutGroupPrototype>() ||
                obj.WasModified<RoleLoadoutPrototype>())
            {
                _profileEditor.RefreshLoadouts();
            }

            if (obj.WasModified<SpeciesPrototype>())
            {
                _profileEditor.RefreshSpecies();
            }

            // Starlight
            // if (obj.WasModified<TraitPrototype>())
            // {
            //     _profileEditor.RefreshTraits();
            // }
        }
        OnAnyCharacterOrJobChange?.Invoke();
    }

    private void PreferencesDataLoaded()
    {
        PreviewPanel?.SetLoaded(true);

        // Starlight begin
        if (TryRegenerateAfterAccept())
            return;
        // Starlight end

        if (_stateManager.CurrentState is not LobbyState)
            return;

        if (_characterSetup != null)
            _characterSetup.SelectedCharacterSlot = null;
        ReloadCharacterSetup();
    }

    public void OnStateEntered(LobbyState state)
    {
        PreviewPanel?.SetLoaded(_preferencesManager.ServerDataLoaded);
        if (_characterSetup != null)
            _characterSetup.SelectedCharacterSlot = null;
        ReloadCharacterSetup();
    }

    public void OnStateExited(LobbyState state)
    {
        PreviewPanel?.SetLoaded(false);

        if (_stateManager.CurrentState is LobbyState lobby)
        {
            lobby.Lobby?.CharacterSetupState.RemoveAllChildren();
        }
    }

    /// <summary>
    /// Reloads every single character setup control.
    /// </summary>
    public void ReloadCharacterSetup()
    {
        RefreshLobbyPreview();
        var (characterGui, profileEditor) = EnsureGui();
        characterGui.ReloadCharacterPickers();
        profileEditor.ResetToDefault();
        _jobPriorityEditor?.LoadJobPriorities();
        characterGui.UpdatePendingReview(); // Starlight
    }

    /// <summary>
    /// Refreshes the character preview in the lobby chat.
    /// </summary>
    private void RefreshLobbyPreview()
    {
        PreviewPanel?.Refresh();
    }

    private void RefreshEditors()
    {
        _profileEditor?.RefreshAntags();
        _profileEditor?.RefreshJobs();
        _profileEditor?.RefreshLoadouts();
        _jobPriorityEditor?.RefreshJobs();
    }

    /// <summary>
    /// Save job priorities locally and on the remote server, reload the character setup gui appropriately
    /// </summary>
    private void SaveJobPriorities()
    {
        if (_jobPriorityEditor == null)
            return;
        SaveJobPriorities(_jobPriorityEditor.SelectedJobPriorities);
    }

    /// <summary>
    /// Save job priorities locally and on the remote server, reload the character setup gui appropriately
    /// </summary>
    /// <param name="newJobPriorities"></param>
    private void SaveJobPriorities(Dictionary<ProtoId<JobPrototype>, JobPriority> newJobPriorities)
    {
        _preferencesManager.UpdateJobPriorities(newJobPriorities);
        OnAnyCharacterOrJobChange?.Invoke();
        _jobPriorityEditor?.LoadJobPriorities();
        var (characterGui, _) = EnsureGui();
        characterGui.ReloadCharacterPickers(selectJobPriorities: true);
    }

    private void SaveProfile()
    {
        DebugTools.Assert(EditedProfile != null);

        if (EditedProfile == null || EditedSlot == null)
            return;

        var fixedProfile = EditedProfile.Clone();
        if(_preferencesManager.Preferences!.TryGetHumanoidInSlot(EditedSlot.Value, out var humanoid))
            fixedProfile = new HumanoidCharacterProfile(EditedProfile) { Enabled = humanoid.Enabled };
        _preferencesManager.UpdateCharacter(fixedProfile, EditedSlot.Value);

        OnAnyCharacterOrJobChange?.Invoke();
        _profileEditor?.SetProfile(EditedSlot.Value);
        ReloadCharacterSetup();
    }

    private void CloseProfileEditor()
    {
        if (_profileEditor == null)
            return;

        _profileEditor.SetProfile(null, null);
        _profileEditor.Visible = false;

        if (_stateManager.CurrentState is LobbyState lobbyGui)
        {
            lobbyGui.SwitchState(LobbyGui.LobbyGuiState.Default);
        }

        // make sure the window is closed.
        _characterSetupWindow?.DisposePopOut(); // Starlight: also close the popped-out desktop window if any
        _characterSetupWindow?.Close(); // starlight

        StopPendingReview(); // Starlight
        RefreshLobbyPreview();
    }

    private void OpenSavePanel(Action saveAction)
    {
        if (_savePanel is { IsOpen: true })
            return;

        _savePanel = new CharacterSetupGuiSavePanel();

        _savePanel.SaveButton.OnPressed += _ =>
        {
            saveAction?.Invoke();

            _savePanel.Close();

            CloseProfileEditor();
        };

        _savePanel.NoSaveButton.OnPressed += _ =>
        {
            _savePanel.Close();

            CloseProfileEditor();
        };

        _savePanel.OpenCentered();
    }

    private (CharacterSetupGui, HumanoidProfileEditor) EnsureGui()
    {
        /*
         * Starlight: Moved up here amd orphaned beforehand to """fix""" a bug with the priority selector.
         * TL;DR: Move this underneath the check below, open the UI, then open it again. Button selectors stop visually
         * updating which ones are NOT selected so it all becomes green.
         * TODO: Find a proper fix to the bug that doesn't involve regenerating this every time.
         */
        _jobPriorityEditor?.Orphan();
        _jobPriorityEditor = new JobPriorityEditor();
        _jobPriorityEditor.Save += SaveJobPriorities;

        if (_characterSetup != null && _profileEditor != null)
        {
            _characterSetup.Visible = true;
            _profileEditor.Visible = true;
            // begin starlight
            // we borrow the gui from the lobby, so we need to make sure we return it aswell.
            if (_stateManager.CurrentState is LobbyState lobbyState
                && lobbyState.Lobby?.CharacterSetupState is { } container
                && _characterSetup.Parent != container)
            {
                // if the ghost window is open return it.
                _characterSetupWindow?.DisposePopOut(); // Starlight: close the popout
                _characterSetupWindow?.Close();
                _characterSetup.Orphan();
                container.AddChild(_characterSetup);
            }
            _characterSetup.JobPriorityEditor.AddChild(_jobPriorityEditor);
            // end starlight
            return (_characterSetup, _profileEditor);
        }

        _profileEditor = new HumanoidProfileEditor(
            _preferencesManager,
            _configurationManager,
            EntityManager,
            _dialogManager,
            LogManager,
            _playerManager,
            _prototypeManager,
            _resourceCache,
            _requirements,
            _markings);

        _profileEditor.OnOpenGuidebook += _guide.OpenHelp;

        _characterSetup = new CharacterSetupGui(_profileEditor, _jobPriorityEditor);

        _characterSetup.CloseButton.OnPressed += _ =>
        {
            // Starlight begin
            if (_preferencesManager.ReviewingPending)
            {
                CloseProfileEditor();
                return;
            }
            // Starlight end

            // Open the save panel if we have unsaved changes.
            if( _profileEditor.Visible && _profileEditor.Profile != null && _profileEditor.IsDirty)
            {
                OpenSavePanel(SaveProfile);

                return;
            }

            if (_characterSetup.JobPriorityEditor.GetChild(0).Visible && ((JobPriorityEditor)_characterSetup.JobPriorityEditor.GetChild(0)).IsDirty()) // Starlight edit
            {
                OpenSavePanel(SaveJobPriorities);

                return;
            }

            // Reset sliders etc.
            CloseProfileEditor();
        };

        _profileEditor.Save += SaveProfile;

        // Starlight begin
        _characterSetup.PendingToggleButton.OnPressed += _ => TogglePendingView();
        _characterSetup.PendingAcceptButton.OnPressed += _ => AcceptPendingPreferences();
        _characterSetup.PendingRejectButton.OnPressed += _ => OpenPendingRejectWindow();
        // Starlight end

        _characterSetup.SelectCharacter += args =>
        {
            _profileEditor.SetProfile(args);
            if (_characterSetup != null)
                _characterSetup.SelectedCharacterSlot = args;
            ReloadCharacterSetup();
        };

        _characterSetup.DeleteCharacter += args =>
        {
            _preferencesManager.DeleteCharacter(args);

            // Reload everything
            if (EditedSlot == args)
            {
                ReloadCharacterSetup();
            }
            else
            {
                // Only need to reload character pickers
                _characterSetup?.ReloadCharacterPickers();
            }
        };

        _characterSetup.SetCharacterEnable += args =>
        {
            _preferencesManager.SetCharacterEnable(args.Item1, args.Item2);
            OnAnyCharacterOrJobChange?.Invoke();
            _characterSetup?.ReloadCharacterPickers();
        };

        if (_stateManager.CurrentState is LobbyState lobby)
        {
            lobby.Lobby?.CharacterSetupState.AddChild(_characterSetup);
        }

        return (_characterSetup, _profileEditor);
    }
}
