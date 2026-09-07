using Content.Client.Gameplay;
using Content.Client._Starlight.UI;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Railroading;

[UsedImplicitly]
public sealed class CardsUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<RailroadingSystem>
{
    private static readonly Color _pulseColor = Color.FromHex("#80FF75");
    private const float PulsePeriod = 1.4f;

    private RailroadingSystem? _railroading;
    private SLWindow? _noCardsWindow;
    private float _pulse;

    private MenuButton? CharacterButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.CharacterButton;

    /// <summary>
    /// Whether the local player has railroading cards waiting to be picked.
    /// </summary>
    public bool CardsPending => _railroading?.CardsPending ?? false;

    /// <summary>
    /// Tint shared by every cards-related button, so they all pulse in step.
    /// </summary>
    public Color PulseModulate { get; private set; } = Color.White;

    public void OnStateEntered(GameplayState state)
    {
    }

    public void OnStateExited(GameplayState state)
    {
        ResetPulse();

        _noCardsWindow?.Close();
        _noCardsWindow = null;
    }

    public void OnSystemLoaded(RailroadingSystem system) => _railroading = system;

    public void OnSystemUnloaded(RailroadingSystem system)
    {
        _railroading = null;
        ResetPulse();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        if (CardsPending)
        {
            _pulse += args.DeltaSeconds;
            var blend = (MathF.Sin(_pulse / PulsePeriod * MathF.Tau) + 1f) / 2f;
            PulseModulate = Color.InterpolateBetween(Color.White, _pulseColor, blend);
        }
        else
        {
            _pulse = 0f;
            PulseModulate = Color.White;
        }

        if (CharacterButton is { } button && button.Modulate != PulseModulate)
            button.Modulate = PulseModulate;
    }

    /// <summary>
    /// Opens the card selection window, or a placeholder when there is nothing to pick.
    /// </summary>
    public void OpenCards()
    {
        if (CardsPending)
        {
            _railroading?.RequestCardSelection();
            return;
        }

        _noCardsWindow ??= new SLWindow();
        CardWindow.RenderEmpty(_noCardsWindow, _railroading?.CardsRestricted ?? false);
        _noCardsWindow.OpenCentered();
    }

    private void ResetPulse()
    {
        _pulse = 0f;
        PulseModulate = Color.White;

        if (CharacterButton is { } button)
            button.Modulate = Color.White;
    }
}
