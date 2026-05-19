using Content.Client.UserInterface.Systems.Bwoink;
using Robust.Client.UserInterface.Controllers;
using Content.Client._Starlight.MHelp.UI;
using Content.Client.Stylesheets;

namespace Content.Client._Starlight.MHelp;

public sealed class StaffHelpUIController : UIController
{
    [Dependency] private readonly AHelpUIController _aHelp = default!;
    [Dependency] private readonly MHelpUIController _mHelp = default!;

    private StaffHelpWindow? _staffHelpWindow;

    public void ToggleWindow()
    {
        if (_staffHelpWindow != null)
        {
            _staffHelpWindow.Close();
            _staffHelpWindow = null;
            SetAHelpButtonPressed(false);
            return;
        }

        SetAHelpButtonPressed(true);
        _staffHelpWindow = new StaffHelpWindow();
        _staffHelpWindow.OnClose += () => _staffHelpWindow = null;
        _staffHelpWindow.OpenCentered();
        UIManager.ClickSound();

        if (_mHelp._hasUnreadMHelp)
            _staffHelpWindow.MentorHelpButton.StyleClasses.Add(StyleClass.Negative);

        if (_aHelp._hasUnreadAHelp)
            _staffHelpWindow.AdminHelpButton.StyleClasses.Add(StyleClass.Negative);

        _staffHelpWindow.AdminHelpButton.OnPressed += _ =>
        {
            _aHelp.Open();
            _staffHelpWindow.Close();
            _aHelp._hasUnreadAHelp = false;
            SetAHelpButtonPressed(false);
        };

        _staffHelpWindow.MentorHelpButton.OnPressed += _ =>
        {
            _mHelp.Open();
            _staffHelpWindow.Close();
            _mHelp._hasUnreadMHelp = false;
            SetAHelpButtonPressed(false);
        };
    }

    public void RefreshAhelpButton()
    {
        if (_mHelp._hasUnreadMHelp || _aHelp._hasUnreadAHelp)
        {
            _aHelp.GameAHelpButton?.StyleClasses.Add(StyleClass.Negative);
            _aHelp.LobbyAHelpButton?.StyleClasses.Add(StyleClass.Negative);
        }
        else
        {
            _aHelp.GameAHelpButton?.StyleClasses.Remove(StyleClass.Negative);
            _aHelp.LobbyAHelpButton?.StyleClasses.Remove(StyleClass.Negative);
        }
    }

    private void SetAHelpButtonPressed(bool pressed)
    {
        _aHelp.GameAHelpButton?.Pressed = pressed;

        _aHelp.LobbyAHelpButton?.Pressed = pressed;

        UIManager.ClickSound();
        RefreshAhelpButton();
    }
}
