using System.Linq;
using System.Numerics;
using Content.Client._Starlight.Lobby.UI;
using Content.Client._Starlight.UserInterface;
using Content.Client.Lobby.UI;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    /// <summary>
    /// character editor window, see OpenCharacterSetupWindow()
    /// </summary>
    private PopOutWindow? _characterSetupWindow;
    private bool _characterSetupPoppedOut;
    private JobPrioritiesWindow? _jobPrioritiesWindow;

    /// <summary>
    /// Opens the character editor in its own window, we reuse the one from the lobby for the sake of simplicity.
    /// </summary>
    public void OpenCharacterSetupWindow()
    {
        // don't open it more than once.
        if (_characterSetupWindow is { IsOpen: true } || _characterSetupPoppedOut)
        {
            _characterSetupWindow?.MoveToFront();
            return;
        }

        var (characterGui, _) = EnsureGui();

        // detach the gui from it's parent (Most of the time the lobby)
        characterGui.Orphan();

        var window = new CharacterSetupWindow(characterGui, RequestCloseCharacterSetup)
        {
            Title = Loc.GetString("ghost-gui-character-editor-button"),
            SetSize = new Vector2(1200, 720),
            MinSize = new Vector2(960, 560),
        };

        window.Contents.AddChild(characterGui);

        // reload these, the lobby button does this so we do it aswell.
        characterGui.ReloadCharacterPickers();
        _profileEditor?.ResetToDefault();
        _jobPriorityEditor?.LoadJobPriorities();

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

        window.OnPopout += () => _characterSetupPoppedOut = true;

        _characterSetupWindow = window;
        window.OpenCentered();

        if (_configurationManager.GetCVar(StarlightCCVars.CharacterSetupMaximized))
            window.SetMaximized(true);
        window.MaximizedChanged += maximized =>
        {
            _configurationManager.SetCVar(StarlightCCVars.CharacterSetupMaximized, maximized);
            _configurationManager.SaveToFile();
        };
    }

    /// <summary>
    /// Sets the job priorities and updates all relevant UI elements. This is called when job priorities are changed in the job priorities window or other sources.
    /// </summary>
    public void SetJobPriorities(Dictionary<ProtoId<JobPrototype>, JobPriority> priorities, Control source)
    {
        _preferencesManager.UpdateJobPriorities(priorities);
        OnAnyCharacterOrJobChange?.Invoke();
        _jobPriorityEditor?.LoadJobPriorities();
        PreviewPanel?.Refresh();

        if (source != _jobPrioritiesWindow)
            _jobPrioritiesWindow?.Refresh();

        var columns = (_stateManager.CurrentState as LobbyState)?.Lobby?.JobColumns;
        if (source != columns)
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

    private sealed class CharacterSetupWindow : PopOutWindow
    {
        protected override Control Control { get; }

        private readonly CharacterSetupGui _gui;
        private readonly Func<bool> _requestClose;

        public CharacterSetupWindow(CharacterSetupGui content, Func<bool> requestClose)
        {
            Control = content;
            _gui = content;
            _requestClose = requestClose;

            content.HeaderBar.Visible = false;
            OnPopout += () => _gui.HeaderBar.Visible = true;
            OnFinalClose += () => _gui.HeaderBar.Visible = true;
            AddMaximizeButton();
        }

        private const string MaximizeIcon = "/Textures/_Starlight/Interface/Nano/maximize.png";
        private const string RestoreIcon = "/Textures/_Starlight/Interface/Nano/restore.png";

        private TextureButton? _maximizeButton;
        private bool _maximized;
        private Vector2 _restorePosition;
        private Vector2 _restoreSize;
        private Vector2 _fittedTo;

        private void AddMaximizeButton()
        {
            if (!WindowHeader.Children.OfType<BoxContainer>().TryFirstOrDefault(out var header))
                return;

            _maximizeButton = new TextureButton
            {
                // Colored like the pop out button next to it, the texture is set here.
                StyleClasses = { PopOutExtensions.PopOutButtonStyleClass },
                TexturePath = MaximizeIcon,
                VerticalAlignment = VAlignment.Center,
                ToolTip = Loc.GetString("ui-window-maximize"),
                Margin = new Thickness(0, 0, 6, 0),
            };
            _maximizeButton.OnPressed += _ => SetMaximized(!_maximized);
            header.AddChild(_maximizeButton);
            // Before the pop out and close buttons.
            _maximizeButton.SetPositionInParent(header.ChildCount - 3);
        }

        /// <summary>
        /// Raised when the player fills the screen with the window or puts it back, to remember for next time.
        /// </summary>
        public event Action<bool>? MaximizedChanged;

        public void SetMaximized(bool maximized)
        {
            if (Parent == null || _maximizeButton == null || maximized == _maximized)
                return;

            _maximized = maximized;
            if (maximized)
            {
                // Opened full screen, the window hasn't been laid out yet: put it back at its set size, centered.
                _restoreSize = Size.X > 0 && Size.Y > 0 ? Size : SetSize;
                _restorePosition = Size.X > 0 && Size.Y > 0 ? Position : (Parent.Size - _restoreSize) / 2;
                Fit();
            }
            else
            {
                SetSize = _restoreSize;
                LayoutContainer.SetPosition(this, _restorePosition);
            }

            _maximizeButton.TexturePath = maximized ? RestoreIcon : MaximizeIcon;
            _maximizeButton.ToolTip = Loc.GetString(maximized ? "ui-window-restore" : "ui-window-maximize");
            MaximizedChanged?.Invoke(maximized);
        }

        private void Fit()
        {
            if (Parent == null)
                return;

            _fittedTo = Parent.Size;
            SetSize = Parent.Size;
            LayoutContainer.SetPosition(this, Vector2.Zero);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            // Stays full screen when the game window is resized.
            if (_maximized && Parent != null && Parent.Size != _fittedTo)
                Fit();
        }

        public override void Close()
        {
            if (Control.Parent != null && CancelClose())
                return;

            base.Close();
        }

        /// <summary>
        /// Asks about unsaved changes, both for the in-game window and the popped out desktop one.
        /// </summary>
        protected override bool CancelClose() => _requestClose();
    }
}
