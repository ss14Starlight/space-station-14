using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Content.Shared._Starlight.Achievement;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Achievement;

[UsedImplicitly]
public sealed class AchievementUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IClientAchievementManager _achievements = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private MenuButton? AchievementButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.AchievementButton;

    private AchievementWindow? _window;
    private DefaultWindow? _notification;

    public void OnStateEntered(GameplayState state)
    {
        _window = UIManager.CreateWindow<AchievementWindow>();

        if (AchievementButton != null)
        {
            _window.OnClose += () => AchievementButton.Pressed = false;
            _window.OnOpen += () =>
            {
                AchievementButton.Pressed = true;
                _window.Populate(_protoManager, _achievements);
            };
        }

        _achievements.AchievementUnlocked += OnAchievementUnlocked;
        _achievements.AchievementsUpdated += OnAchievementsUpdated;
    }

    public void OnStateExited(GameplayState state)
    {
        _achievements.AchievementUnlocked -= OnAchievementUnlocked;
        _achievements.AchievementsUpdated -= OnAchievementsUpdated;

        _window?.Close();
        _window?.Dispose();
        _window = null;

        _notification?.Close();
        _notification?.Dispose();
        _notification = null;
    }

    public void LoadButton()
    {
        if (AchievementButton != null)
            AchievementButton.OnPressed += OnButtonPressed;
    }

    public void UnloadButton()
    {
        if (AchievementButton != null)
            AchievementButton.OnPressed -= OnButtonPressed;
    }

    private void OnButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
            _window.Close();
        else
            _window.OpenCentered();
    }

    private void OnAchievementsUpdated()
    {
        if (_window is { IsOpen: true })
            _window.Populate(_protoManager, _achievements);
    }

    private void OnAchievementUnlocked(string achievementId)
    {
        if (!_protoManager.TryIndex<AchievementPrototype>(achievementId, out var proto))
            return;

        _notification?.Close();
        _notification?.Dispose();

        _notification = new DefaultWindow
        {
            Title = Loc.GetString("achievement-notification-title"),
        };

        var label = new Label
        {
            Text = Loc.GetString("achievement-notification-body", ("name", Loc.GetString(proto.Name))),
        };
        _notification.Contents.AddChild(label);
        _notification.OpenCentered();

        Timer.Spawn(5000, () =>
        {
            _notification?.Close();
            _notification?.Dispose();
            _notification = null;
        });
    }
}
