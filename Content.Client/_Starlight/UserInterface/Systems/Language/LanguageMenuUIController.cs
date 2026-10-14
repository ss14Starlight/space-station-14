using JetBrains.Annotations;
using Content.Client._Starlight.Language;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;

namespace Content.Client._Starlight.UserInterface.Systems.Language;

[UsedImplicitly]
public sealed class LanguageMenuUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    public LanguageMenuWindow? LanguageWindow;
    private MenuButton? _languageButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.LanguageButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(LanguageWindow == null);

        LanguageWindow = UIManager.CreateWindow<LanguageMenuWindow>();
        LayoutContainer.SetAnchorPreset(LanguageWindow, LayoutContainer.LayoutPreset.CenterTop);

        LanguageWindow.OnClose += ()
            => _languageButton?.Pressed = false;
        LanguageWindow.OnOpen += ()
            => _languageButton?.Pressed = true;

        CommandBinds.Builder.Bind(ContentKeyFunctions.OpenLanguageMenu,
            InputCmdHandler.FromDelegate(_ => ToggleWindow())).Register<LanguageMenuUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        LanguageWindow?.Dispose();
        LanguageWindow = null;

        CommandBinds.Unregister<LanguageMenuUIController>();
    }

    public void UnloadButton()
    {
        if (_languageButton == null)
            return;

        _languageButton.OnPressed -= LanguageButtonPressed;
    }

    public void LoadButton()
    {
        if (_languageButton == null)
            return;

        _languageButton.OnPressed += LanguageButtonPressed;
    }

    private void LanguageButtonPressed(ButtonEventArgs args)
        => ToggleWindow();

    private void ToggleWindow()
    {
        if (LanguageWindow == null)
            return;

        _languageButton?.SetClickPressed(!LanguageWindow.IsOpen);

        if (LanguageWindow.IsOpen)
            LanguageWindow.Close();
        else
            LanguageWindow.Open();
    }
}
