using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Actions.UI;

/// <summary>
/// HUD banner shown to both parties of an active latch: title, progress bar,
/// a smaller max-duration countdown bar, and instruction line.
/// </summary>
public sealed class LatchStatusControl : PanelContainer
{
    private readonly Label _title;
    private readonly ProgressBar _bar;
    private readonly ProgressBar _maxBar;
    private readonly Label _instruction;

    public LatchStatusControl()
    {
        MinWidth = 260;
        Margin = new Thickness(0, 8, 0, 0);
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(0, 0, 0, 180),
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };

        var layout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        _title = new Label
        {
            Text = "LATCHED",
            Align = Label.AlignMode.Center,
            FontColorOverride = Color.OrangeRed,
        };

        _bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MinHeight = 16,
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.OrangeRed },
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = new Color(40, 40, 40) },
        };

        _maxBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MinHeight = 6,
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.White },
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = new Color(40, 40, 40) },
        };

        _instruction = new Label
        {
            Align = Label.AlignMode.Center,
            FontColorOverride = Color.LightGray,
        };

        layout.AddChild(_title);
        layout.AddChild(_bar);
        layout.AddChild(_maxBar);
        layout.AddChild(_instruction);
        AddChild(layout);

        Visible = false;
    }

    /// <summary>
    /// Updates both bars and the instruction text, and shows the control.
    /// </summary>
    /// <param name="fraction">Remaining time before the latch's current end time, 0 to 1.</param>
    /// <param name="maxFraction">Remaining time before the latch's fixed hard cap, 0 to 1.</param>
    public void UpdateState(float fraction, float maxFraction, string instruction)
    {
        Visible = true;
        _bar.Value = fraction;
        _maxBar.Value = maxFraction;
        _instruction.Text = instruction;
    }

    public void Hide()
    {
        Visible = false;
    }
}
