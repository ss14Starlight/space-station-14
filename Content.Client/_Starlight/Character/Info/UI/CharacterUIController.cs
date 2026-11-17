using System.Linq;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using Content.Client.Message;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Client.UserInterface.Systems.Objectives.Controls;
using Content.Client._Starlight.Character.Info;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._Starlight.Character.Info.UI;

[UsedImplicitly]
public sealed partial class CharacterUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private IPlayerManager _player = default!;

    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;
    [UISystemDependency] private readonly SLCharacterWindowSystem _characterWindow = default!;
    [UISystemDependency] private readonly SpriteSystem _sprite = default!;

    private const int DescriptionWordLimit = 40;
    private float _objectiveRefreshTimer;
    private int _objectiveCount;

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_window is not { IsOpen: true })
            return;
        _objectiveRefreshTimer += args.DeltaSeconds;
        if (_objectiveRefreshTimer < 1)
            return;
        _objectiveRefreshTimer = 0;
        _window.AddObjectiveButtons(_objectiveCount,
            _characterWindow.CanPickObjectives(_player.LocalEntity),
            _characterWindow.CanPickMoreObjectives(_player.LocalEntity));
    }

    private SLCharacterWindow? _window;
    private MenuButton? _characterButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.CharacterButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);

        _window = UIManager.CreateWindow<SLCharacterWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Center);
        LayoutContainer.SetGrowHorizontal(_window, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetGrowVertical(_window, LayoutContainer.GrowDirection.Both);

        _window.OnClose += DeactivateButton;
        _window.OnOpen += ActivateButton;
        _window.OnClose += ClearSelfCharacterInfo;
        _window.OnOpen += () => SetSelfCharacterInfo(_player.LocalEntity);

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

        var (entity, jobTitle, objectives, briefing, entityName) = data;

        _window.SpriteView.SetEntity(entity);

        UpdateRoleType();
        _window.NameLabel.SetMarkup(Loc.GetString("character-info-name-format",
            ("name", FormattedMessage.EscapeText(entityName))));
        var job = _characterWindow.GetJobInfo(entity);
        _window.JobContainer.Visible = job != null;
        if (job is { } jobInfo)
        {
            _window.SubText.SetMarkup(Loc.GetString("character-info-job-format", ("job", FormattedMessage.EscapeText(jobTitle))));
            _window.JobIcon.Texture = jobInfo.Icon;
        }

        var profile = _characterWindow.GetProfileInfo(entity);
        _window.CharacterInfo.Visible = profile != null;
        if (profile is { } profileInfo)
        {
            _window.CharacterInfo.SetMessage(Loc.GetString("character-info-details-format",
                ("gender", profileInfo.Gender),
                ("age", profileInfo.Age),
                ("species", profileInfo.Species)));
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
        SetSelfCharacterInfo(entity);

        var canPickObjectives = _characterWindow.CanPickObjectives(_player.LocalEntity);
        _objectiveCount = objectives.Sum(group => group.Value.Count);
        _window.AddObjectiveButtons(_objectiveCount, canPickObjectives,
            _characterWindow.CanPickMoreObjectives(_player.LocalEntity));

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
            briefingControl.Label.SetMessage(FormattedMessage.FromMarkupPermissive(briefing), Color.Yellow);
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

        SetCenteredText(_window.RoleType, Loc.GetString(roleType.Name), roleType.Color,
            "character-info-role-type-format", "role");
        SetCenteredText(_window.AlignmentLabel, Loc.GetString(roleType.Alignment), roleType.AlignmentColor,
            "character-info-alignment-format", "alignment");
    }

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

    private readonly Dictionary<EntityUid, CharacterInspectWindow> _openInspectionWindows = new();

    public void OpenInspectCharacterWindow(EntityUid target, EntityUid viewer)
    {
        if (!target.Valid)
            return;

        if (target == viewer)
        {
            OpenWindow();
            return;
        }

        if (_openInspectionWindows.TryGetValue(target, out var window))
        {
            window.SetCharacter(target, EntityManager, viewer);
            window.OpenCentered();
            return;
        }

        window = new CharacterInspectWindow();
        window.SetCharacter(target, EntityManager, viewer.Valid ? viewer : target);

        _openInspectionWindows[target] = window;

        window.OnClose += () => _openInspectionWindows.Remove(target);
        window.Title = Loc.GetString("character-info-window-title", ("player", target));
        window.OpenCentered();
    }

    /// <summary>
    /// Opens the local player's character window on its overview tab.
    /// </summary>
    public void OpenCharacterOverview()
    {
        if (_window == null)
            return;

        _window.CharacterInfoTabs.CurrentTab = 0;
        OpenWindow();
    }

    private void ClearSelfCharacterInfo()
    {
        if (_window == null)
            return;
        _window.InfoIC.ClearCharacter();
        _window.InfoOOC.ClearCharacter();
        _window.InfoBackground.ClearCharacter();
    }

    private void SetSelfCharacterInfo(EntityUid? ent)
    {
        if (_window == null)
            return;

        if (!ent.HasValue || !_window.IsOpen)
        {
            ClearSelfCharacterInfo();
            return;
        }

        _window.InfoIC.SetCharacter(ent, EntityManager, ent.Value);
        _window.InfoOOC.SetCharacter(ent, EntityManager, ent);
        _window.InfoBackground.SetCharacter(ent, EntityManager, ent);
    }

    /// <summary>
    /// Wraps styled words into centered rows while preserving the role's font and color.
    /// </summary>
    private static void SetCenteredText(WrapContainer container, string text, Color color, LocId format, string argument)
    {
        container.RemoveAllChildren();
        foreach (var word in text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries))
        {
            container.AddChild(new RichTextLabel
            {
                Text = Loc.GetString(format,
                    ("color", color.ToHex()),
                    (argument, FormattedMessage.EscapeText(word))),
            });
        }
    }
}
