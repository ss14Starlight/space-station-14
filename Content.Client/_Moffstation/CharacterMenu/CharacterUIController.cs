using System.Linq;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using Content.Client.Message;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Objectives.Controls;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._Moffstation.CharacterMenu;

[UsedImplicitly]
public sealed partial class CharacterUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private IPlayerManager _player = default!;

    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;
    [UISystemDependency] private readonly MoffCharacterWindowSystem _characterWindow = default!;
    [UISystemDependency] private readonly SpriteSystem _sprite = default!;

    private const int DescriptionWordLimit = 40;

    private MoffCharacterWindow? _window;
    private MenuButton? _characterButton => UIManager.GetActiveUIWidgetOrNull<UserInterface.Systems.MenuBar.Widgets.GameTopMenuBar>()?.CharacterButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);

        _window = UIManager.CreateWindow<MoffCharacterWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Center);
        LayoutContainer.SetGrowHorizontal(_window, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetGrowVertical(_window, LayoutContainer.GrowDirection.Both);

        _window.OnClose += DeactivateButton;
        _window.OnOpen += ActivateButton;
        SLInitializeCharacterWindow(); // Starlight

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenCharacterMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<CharacterUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        _window?.Close();
        _window = null;

        CommandBinds.Unregister<CharacterUIController>();
    }

    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += CharacterUpdated;
        _player.LocalPlayerDetached += CharacterDetached;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= CharacterUpdated;
        _player.LocalPlayerDetached -= CharacterDetached;
    }

    public void UnloadButton()
    {
        if (_characterButton == null)
        {
            return;
        }

        _characterButton.OnPressed -= CharacterButtonPressed;
    }

    public void LoadButton()
    {
        if (_characterButton == null)
        {
            return;
        }

        _characterButton.OnPressed += CharacterButtonPressed;
    }

    private void DeactivateButton() => _characterButton?.Pressed = false;

    private void ActivateButton() => _characterButton?.Pressed = true;

    private void CharacterUpdated(CharacterInfoSystem.CharacterData data)
    {
        if (_window == null)
        {
            return;
        }

        var (entity, jobTitle, objectives, briefing, entityName) = data; // Starlight

        _window.SpriteView.SetEntity(entity);

        UpdateRoleType();
        _window.NameLabel.SetMarkup(Loc.GetString("character-info-name-format",
            ("name", FormattedMessage.EscapeText(entityName))));
        var job = _characterWindow.GetJobInfo(entity); // Starlight
        _window.JobContainer.Visible = job != null;
        if (job is { } jobInfo)
        {
            _window.SubText.SetMarkup(Loc.GetString("character-info-job-format", ("job", FormattedMessage.EscapeText(jobTitle)))); // Starlight
            _window.JobIcon.Texture = jobInfo.Icon;
        }

        var profile = _characterWindow.GetProfileInfo(entity);
        _window.CharacterInfo.Visible = profile != null;
        if (profile is { } profileInfo)
        {
            _window.CharacterInfo.SetMessage(Loc.GetString("character-info-details-format", // Starlight
                ("gender", profileInfo.Gender),
                ("age", profileInfo.Age),
                ("species", profileInfo.Species))); // Starlight, custom species name.
        }

        var description = _characterWindow.GetDescription(entity);
        _window.DetailedDescription.Visible = description != null;
        if (description != null)
        {
            _window.DetailedDescription.SetMarkupPermissive(Loc.GetString("character-info-description-format",
                ("description", TruncateWords(description, DescriptionWordLimit))));
        }

        _window.Objectives.RemoveAllChildren();
        _window.Briefing.RemoveAllChildren();
        SLSetSelfCharacterInfo(entity); // Starlight

        var canPickObjectives = _characterWindow.CanPickObjectives(_player.LocalEntity);
        _window.AddObjectiveButtons(objectives.Sum(group => group.Value.Count), canPickObjectives); // Starlight

        foreach (var (_, conditions) in objectives)
        {
            foreach (var condition in conditions)
            {
                _window.Objectives.AddChild(new ObjectiveConditionsControl(condition, _sprite));
            }
        }

        if (briefing != null)
        {
            var briefingControl = new ObjectiveBriefingControl();
            var text = new FormattedMessage();
            text.PushColor(Color.Yellow);
            text.AddText(briefing);
            briefingControl.Label.SetMessage(text);
            _window.Briefing.AddChild(briefingControl);
        }

        var controls = _characterInfo.GetCharacterInfoControls(entity);
        foreach (var control in controls)
        {
            _window.Objectives.AddChild(control);
        }

        _window.RolePlaceholder.Visible = false;

        _window.Objectives.InvalidateMeasure();
        _window.ObjectivesWrapper.InvalidateMeasure();
        _window.ObjectivesScroll.InvalidateMeasure();
    }

    public void UpdateRoleType()
    {
        if (_window is not { IsOpen: true } ||
            _characterWindow.GetRoleType(_player.LocalEntity) is not { } roleType)
            return;

        SetRoleType(Loc.GetString(roleType.Name), roleType.Color);
        SetAlignment(roleType.Alignment, roleType.AlignmentColor); // Starlight
    }

    private void SetRoleType(string role, Color color) => _window!.RoleType.Text = Loc.GetString("character-info-role-type-format",
            ("color", color.ToHex()),
            ("role", role));

    private static string TruncateWords(string text, int wordLimit)
    {
        var words = text.Split((char[]?) null, wordLimit + 1, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= wordLimit)
            return text;

        return Loc.GetString("character-info-description-truncated",
            ("description", string.Join(' ', words[..wordLimit])));
    }

    private void CharacterDetached(EntityUid uid) => CloseWindow();

    private void CharacterButtonPressed(ButtonEventArgs args) => ToggleWindow();

    private void CloseWindow() => _window?.Close();

    public void OpenWindow()
    {
        if (_window == null)
            return;

        _characterInfo.RequestCharacterInfo();

        if (_window.IsOpen)
            return;

        _characterButton?.SetClickPressed(true);
        _window.Open();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;

        _characterButton?.SetClickPressed(!_window.IsOpen);

        if (_window.IsOpen)
        {
            CloseWindow();
        }
        else
        {
            _characterInfo.RequestCharacterInfo();
            _window.Open();
        }
    }
}
