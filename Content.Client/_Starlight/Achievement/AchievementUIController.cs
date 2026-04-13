using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Content.Shared._Starlight.Achievement;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Starlight.Achievement;

[UsedImplicitly]
public sealed class AchievementUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    private const int NotificationDisplayDuration = 5000;
    private const float NotificationWidth = 460f;
    private const float NotificationTopOffset = 18f;
    private static readonly SoundPathSpecifier _notificationSound = new("/Audio/Effects/chime.ogg");

    [Dependency] private readonly IClientAchievementManager _achievements = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [UISystemDependency] private readonly AudioSystem _audio = default!;

    private MenuButton? _achievementButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.AchievementButton;

    private AchievementWindow? _window;
    private AchievementNotification? _notification;

    public void OnStateEntered(GameplayState state)
    {
        _window = UIManager.CreateWindow<AchievementWindow>();

        if (_achievementButton != null)
        {
            _window.OnClose += () => _achievementButton.Pressed = false;
            _window.OnOpen += () =>
            {
                _achievementButton.Pressed = true;
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
        _window = null;

        _notification?.Orphan();
        _notification = null;
    }

    public void LoadButton()
    {
        if (_achievementButton != null)
            _achievementButton.OnPressed += OnButtonPressed;
    }

    public void UnloadButton()
    {
        if (_achievementButton != null)
            _achievementButton.OnPressed -= OnButtonPressed;
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

        _notification?.Orphan();

        var notification = _notification = new AchievementNotification();
        notification.SetAchievement(proto);
        notification.CloseRequested += () =>
        {
            notification.Orphan();
            if (_notification == notification)
                _notification = null;
        };
        UIManager.WindowRoot.AddChild(notification);
        LayoutContainer.SetAnchorPreset(notification, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetPosition(notification,
            new Vector2(MathF.Max((UIManager.WindowRoot.Width - NotificationWidth) / 2f, 0f), NotificationTopOffset));

        _audio.PlayGlobal(_notificationSound, Filter.Local(), false,
            AudioParams.Default.WithVolume(-2f));

        Timer.Spawn(NotificationDisplayDuration, () =>
        {
            notification.Orphan();

            if (_notification == notification)
                _notification = null;
        });
    }
}
