using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Actions.UI;

/// <summary>
/// Floating banner above the local player during an active latch.
/// </summary>
public sealed class LatchStatusControl : PanelContainer
{
    private const int PanelWidth = 260;
    private const int LabelWidth = 42;

    // Default stylesheet ProgressBar foreground is a muted green; give the
    // hard-cap bar a contrasting amber so the two are distinguishable.
    private static readonly Color MaxBarColor = new(0.55f, 0.45f, 0.2f);

    private readonly Label _title;
    private readonly ProgressBar _bar;
    private readonly ProgressBar _maxBar;
    private readonly Label _instruction;
    private readonly Button _biteHarderButton;

    /// <summary>
    /// Raised when the Bite Harder button is pressed.
    /// </summary>
    public event Action? BiteHarderPressed;

    public LatchStatusControl()
    {
        MouseFilter = MouseFilterMode.Ignore;
        MinWidth = PanelWidth;
        MaxWidth = PanelWidth;
        StyleClasses.Add(StyleClass.TooltipPanel);

        var layout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            MaxWidth = PanelWidth,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _title = new Label
        {
            Text = Loc.GetString("latch-title"),
            Align = Label.AlignMode.Center,
            StyleClasses = { StyleClass.TooltipTitle },
            MouseFilter = MouseFilterMode.Ignore,
        };

        _bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MaxHeight = 6,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _maxBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MaxHeight = 6,
            HorizontalExpand = true,
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = MaxBarColor },
            MouseFilter = MouseFilterMode.Ignore,
        };

        _instruction = new Label
        {
            Align = Label.AlignMode.Center,
            StyleClasses = { StyleClass.TooltipDesc },
            MouseFilter = MouseFilterMode.Ignore,
        };

        _biteHarderButton = new Button
        {
            Text = Loc.GetString("latch-bite-harder-button"),
            HorizontalExpand = true,
            Visible = false,
        };
        _biteHarderButton.OnPressed += _ => BiteHarderPressed?.Invoke();

        layout.AddChild(_title);
        layout.AddChild(_instruction);
        layout.AddChild(_biteHarderButton);
        layout.AddChild(BarRow(Loc.GetString("latch-label-timeremaining"), _bar));
        layout.AddChild(BarRow(Loc.GetString("latch-label-timemax"), _maxBar));
        AddChild(layout);

        Visible = false;
    }

    private static BoxContainer BarRow(string label, ProgressBar bar)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            MouseFilter = MouseFilterMode.Ignore,
        };

        row.AddChild(new Label
        {
            Text = label,
            MinWidth = LabelWidth,
            StyleClasses = { StyleClass.TooltipDesc },
            MouseFilter = MouseFilterMode.Ignore,
        });
        row.AddChild(bar);

        return row;
    }

    /// <summary>
    /// Updates both bars, the instruction text, and whether the Bite Harder
    /// button is shown (only relevant to the latcher, not the target).
    /// </summary>
    /// <param name="fraction">Remaining time before the current end time, 0 to 1.</param>
    /// <param name="maxFraction">Remaining time before the hard cap, 0 to 1.</param>
    public void UpdateState(float fraction, float maxFraction, string instruction, bool showBiteHarder)
    {
        Visible = true;
        _bar.Value = fraction;
        _maxBar.Value = maxFraction;
        _instruction.Text = instruction;
        _biteHarderButton.Visible = showBiteHarder;
    }

    /// <summary>
    /// Hides the latch status window.
    /// </summary>
    public void Hide() => Visible = false;
}
