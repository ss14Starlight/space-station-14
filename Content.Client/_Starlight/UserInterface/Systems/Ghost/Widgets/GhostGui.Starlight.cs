// ReSharper disable CheckNamespace
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Ghost.Widgets;

public sealed partial class GhostGui
{
    private Button? _roundSummaryButton;

    public void SetReplayMode(bool replay, Action onRoundSummaryPressed)
    {
        // Added in code so the upstream XAML stays untouched.
        if (replay && _roundSummaryButton == null && GhostWarpButton.Parent is { } parent)
        {
            _roundSummaryButton = new Button { Text = Loc.GetString("replay-observer-round-summary-button") };
            _roundSummaryButton.OnPressed += _ => onRoundSummaryPressed();
            parent.AddChild(_roundSummaryButton);
            _roundSummaryButton.SetPositionInParent(GhostWarpButton.GetPositionInParent() + 1);
        }

        _roundSummaryButton?.Visible = replay;

        NewLifeButton.Visible = !replay;
        CharacterEditorButton.Visible = !replay;
        GhostThemeButton.Visible = !replay;
        ReturnToBodyButton.Visible = !replay;
        GhostRolesButton.Visible = !replay;
    }
}
