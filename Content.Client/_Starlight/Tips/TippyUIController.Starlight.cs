using Content.Client._Starlight.Humanoid;
using Content.Shared.Tips;
using Robust.Client.UserInterface;
using static Content.Client.Tips.TippyUI;

// ReSharper disable once CheckNamespace
namespace Content.Client.Tips;

public sealed partial class TippyUIController
{
    [UISystemDependency] private readonly HumanoidAppearanceSystem _appearance = default!;

    private const int MaxQueuedMessages = 3;

    public void AddTippyToQueue(TippyEvent msg)
    {
        // A burst of tippies would otherwise play out one after another for minutes.
        if (_queuedMessages.Count < MaxQueuedMessages)
            _queuedMessages.Enqueue(msg);
    }

    /// <summary>
    /// Drops queued tippies and hides the one on screen right away.
    /// </summary>
    public void ClearTippy()
    {
        _queuedMessages.Clear();
        _currentMessage = null;
        _secondsUntilNextState = 0;
        EntityManager.DeleteEntity(_entity);
        _entity = default;

        if (UIManager.ActiveScreen?.GetWidget<TippyUI>() is not { } tippy)
            return;

        tippy.Visible = false;
        tippy.LabelPanel.Visible = false;
        tippy.State = TippyState.Hidden;
    }
}
