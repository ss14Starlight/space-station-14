using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Admin;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Client.UserInterface.Systems.Character;
using Content.Client.UserInterface.Systems.Crafting;
using Content.Client.UserInterface.Systems.Emotes;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Client.UserInterface.Systems.Sandbox;
using Content.Client._Starlight.UserInterface.Systems.Language; // Starlight
using Content.Client._Starlight.UserInterface.Systems.BugReport; // Starlight
using Content.Client._Starlight.Achievement; // Starlight
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.UserInterface.Systems.MenuBar;

public sealed partial class GameTopMenuBarUIController : UIController
{
    [Dependency] private EscapeUIController _escape = default!;
    [Dependency] private AdminUIController _admin = default!;
    [Dependency] private CharacterUIController _character = default!;
    [Dependency] private CraftingUIController _crafting = default!;
    [Dependency] private AHelpUIController _ahelp = default!;
    [Dependency] private ActionUIController _action = default!;
    [Dependency] private SandboxUIController _sandbox = default!;
    [Dependency] private GuidebookUIController _guidebook = default!;
    [Dependency] private EmotesUIController _emotes = default!;
    [Dependency] private LanguageMenuUIController _language = default!; // Starlight
    [Dependency] private BugReportUIController _bug = default!; // Starlight
    [Dependency] private AchievementUIController _achievement = default!; // Starlight

    private GameTopMenuBar? GameTopMenuBar => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>();

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += LoadButtons;
        gameplayStateLoad.OnScreenUnload += UnloadButtons;
    }

    public void UnloadButtons()
    {
        _escape.UnloadButton();
        _guidebook.UnloadButton();
        _admin.UnloadButton();
        _character.UnloadButton();
        _crafting.UnloadButton();
        _ahelp.UnloadButton();
        _action.UnloadButton();
        _sandbox.UnloadButton();
        _emotes.UnloadButton();
        _language.UnloadButton(); // Starlight
        _bug.UnloadButton(); // Starlight
        _achievement.UnloadButton(); // Starlight
    }

    public void LoadButtons()
    {
        _escape.LoadButton();
        _guidebook.LoadButton();
        _admin.LoadButton();
        _character.LoadButton();
        _crafting.LoadButton();
        _ahelp.LoadButton();
        _action.LoadButton();
        _sandbox.LoadButton();
        _emotes.LoadButton();
        _language.LoadButton(); // Starlight
        _bug.LoadButton(); // Starlight
        _achievement.LoadButton(); // Starlight
    }
}
