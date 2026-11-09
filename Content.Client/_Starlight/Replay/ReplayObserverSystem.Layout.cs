using Content.Client.UserInterface.Systems.Actions.Widgets;
using Robust.Client.Replays.UI;

namespace Content.Client._Starlight.Replay;

// The replay control widget is anchored top-left over the actions bar, so push the bar below it.
public sealed partial class ReplayObserverSystem
{
    private const float ReplayWidgetGap = 10f;

    private ActionsBar? _shiftedActionsBar;
    private float _actionsBarOffset;

    private void UpdateActionsBarOffset()
    {
        var screen = _uiManager.ActiveScreen;
        var actions = screen?.GetWidget<ActionsBar>();
        var replayWidget = screen?.GetWidget<ReplayControlWidget>();

        if (_shiftedActionsBar != null && _shiftedActionsBar != actions)
            ResetActionsBarOffset();

        if (actions == null)
            return;

        var offset = 0f;
        if (replayWidget is { Visible: true })
        {
            var naturalTop = actions.GlobalPosition.Y - _actionsBarOffset;
            var replayBottom = replayWidget.GlobalPosition.Y + replayWidget.Size.Y + ReplayWidgetGap;
            offset = MathF.Max(0f, replayBottom - naturalTop);
        }

        if (MathHelper.CloseTo(offset, _actionsBarOffset))
            return;

        var margin = actions.Margin;
        actions.Margin = new Thickness(margin.Left, margin.Top - _actionsBarOffset + offset, margin.Right, margin.Bottom);
        _actionsBarOffset = offset;
        _shiftedActionsBar = actions;
    }

    private void ResetActionsBarOffset()
    {
        if (_shiftedActionsBar is { } bar)
        {
            var margin = bar.Margin;
            bar.Margin = new Thickness(margin.Left, margin.Top - _actionsBarOffset, margin.Right, margin.Bottom);
        }

        _shiftedActionsBar = null;
        _actionsBarOffset = 0f;
    }
}
