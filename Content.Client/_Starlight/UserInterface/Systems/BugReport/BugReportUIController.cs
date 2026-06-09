using Content.Client._Starlight.UserInterface.Systems.BugReport.Windows;
using Content.Client.Gameplay;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._Starlight.BugReport;
using Content.Shared.Starlight.CCVar;
using JetBrains.Annotations;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.UserInterface.Systems.BugReport;

[UsedImplicitly]
public sealed class BugReportUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceCache _resource = default!;

    // This is the link to the hotbar button
    private MenuButton? _bugReportButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.ReportBugButton;

    // Don't clear this window. It needs to be saved so the input doesn't get erased when it's closed!
    private BugReportWindow _bugReportWindow = default!;

    private readonly ResPath _bug = new("/Textures/_Starlight/Interface/bug.svg.192dpi.png");
    private readonly ResPath _splat = new("/Textures/_Starlight/Interface/splat.svg.192dpi.png");

    public void OnStateEntered(GameplayState state)
        => SetupWindow();

    public void OnStateExited(GameplayState state)
        => CleanupWindow();

    public void LoadButton()
        => _bugReportButton?.OnPressed += ButtonToggleWindow;

    public void UnloadButton()
        => _bugReportButton?.OnPressed -= ButtonToggleWindow;

    private void SetupWindow()
    {
        if (_bugReportButton == null)
            return;

        _bugReportWindow = UIManager.CreateWindow<BugReportWindow>();
        // This is to make sure the hotbar button gets checked and unchecked when the window is opened / closed.
        _bugReportWindow.OnClose += () =>
        {
            _bugReportButton.Pressed = false;
            _bugReportButton.Icon = _resource.GetTexture(_bug);
        };
        _bugReportWindow.OnOpen += () =>
        {
            _bugReportButton.Pressed = true;
            _bugReportButton.Icon = _resource.GetTexture(_splat);
        };

        _bugReportWindow.OnBugReportSubmitted += OnBugReportSubmitted;

        _cfg.OnValueChanged(StarlightCCVars.EnablePlayerBugReports, UpdateButtonVisibility, true);
    }

    private void CleanupWindow()
    {
        _bugReportWindow.CleanupCCvars();

        _cfg.UnsubValueChanged(StarlightCCVars.EnablePlayerBugReports, UpdateButtonVisibility);
    }

    private void ToggleWindow()
    {
        if (_bugReportWindow.IsOpen)
            _bugReportWindow.Close();
        else
            _bugReportWindow.OpenCentered();
    }

    private void OnBugReportSubmitted(PlayerBugReportInformation report)
    {
        var message = new BugReportMessage { ReportInformation = report };
        _net.ClientSendMessage(message);
        _bugReportWindow.Close();
    }

    private void ButtonToggleWindow(BaseButton.ButtonEventArgs obj)
        => ToggleWindow();

    private void UpdateButtonVisibility(bool val)
    {
        if (_bugReportButton == null)
            return;

        _bugReportButton.Visible = val;
    }
}
