using Content.Client.Stylesheets;
using Content.Shared._Starlight.Actions.Components;
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
    private static readonly Color _maxBarColor = new(0.55f, 0.45f, 0.2f);

    // Stands out against the greyed bar so a paused minigame reads as intentional.
    private static readonly Color _blockedTextColor = new(0.95f, 0.65f, 0.25f);

    private readonly Label _title;
    private readonly ProgressBar _bar;
    private readonly ProgressBar _maxBar;
    private readonly Label _instruction;
    private readonly Button _biteHarderButton;
    private readonly BoxContainer _struggleBox;
    private readonly LatchStruggleBar _struggleBar;
    private readonly Label _struggleStatus;
    private readonly Button _struggleButton;

    /// <summary>
    /// Raised when the Bite Harder button is pressed.
    /// </summary>
    public event Action? BiteHarderPressed;

    /// <summary>
    /// Raised when the Struggle button is pressed.
    /// </summary>
    public event Action? StrugglePressed;

    /// <summary>
    /// Name of the key bound to struggle, shown in the hint. Null if unbound.
    /// </summary>
    public string? StruggleKey { get; set; }

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
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = _maxBarColor },
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

        _struggleBar = new LatchStruggleBar();

        _struggleStatus = new Label
        {
            Align = Label.AlignMode.Center,
            StyleClasses = { StyleClass.TooltipDesc },
            MouseFilter = MouseFilterMode.Ignore,
        };

        _struggleButton = new Button
        {
            Text = Loc.GetString("latch-struggle-button"),
            HorizontalExpand = true,
        };
        _struggleButton.OnPressed += _ => StrugglePressed?.Invoke();

        _struggleBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        };
        _struggleBox.AddChild(_struggleBar);
        _struggleBox.AddChild(_struggleStatus);
        _struggleBox.AddChild(_struggleButton);

        layout.AddChild(_title);
        layout.AddChild(_instruction);
        layout.AddChild(_biteHarderButton);
        layout.AddChild(_struggleBox);
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
    /// Updates the struggle minigame. Only shown to the latch target.
    /// </summary>
    /// <param name="cursor">Cursor position on the bar, 0 to 1.</param>
    /// <param name="result">Press result to show, or None while live.</param>
    /// <param name="block">Why struggling is paused, or None.</param>
    /// <param name="canPress">Whether the Struggle button is currently enabled.</param>
    /// <param name="shakeOffset">Horizontal bar shake from Bite Harder, in UI units.</param>
    public void UpdateStruggle(
        float zoneCenter,
        float perfectWidth,
        float goodWidth,
        float cursor,
        LatchStruggleResult result,
        LatchStruggleBlock block,
        bool canPress,
        float shakeOffset)
    {
        _struggleBox.Visible = true;

        _struggleBar.ZoneCenter = zoneCenter;
        _struggleBar.PerfectWidth = perfectWidth;
        _struggleBar.GoodWidth = goodWidth;
        _struggleBar.Cursor = cursor;
        _struggleBar.Result = result;
        _struggleBar.Paused = block != LatchStruggleBlock.None;
        _struggleBar.ShakeOffset = shakeOffset;

        _struggleStatus.Text = block switch
        {
            LatchStruggleBlock.Exhausted => Loc.GetString("latch-struggle-exhausted"),
            LatchStruggleBlock.Stunned => Loc.GetString("latch-struggle-stunned"),
            LatchStruggleBlock.Incapacitated => Loc.GetString("latch-struggle-incapacitated"),
            _ => result switch
            {
                LatchStruggleResult.Perfect => Loc.GetString("latch-struggle-perfect"),
                LatchStruggleResult.Good => Loc.GetString("latch-struggle-good"),
                LatchStruggleResult.Miss => Loc.GetString("latch-struggle-miss"),
                _ => StruggleKey is null
                    ? Loc.GetString("latch-struggle-hint")
                    : Loc.GetString("latch-struggle-hint-key", ("key", StruggleKey)),
            },
        };
        _struggleStatus.FontColorOverride = block != LatchStruggleBlock.None ? _blockedTextColor : null;

        _struggleButton.Disabled = !canPress;
    }

    /// <summary>
    /// Hides the struggle minigame (e.g. for the latcher's own panel).
    /// </summary>
    public void HideStruggle() => _struggleBox.Visible = false;

    /// <summary>
    /// Hides the latch status window.
    /// </summary>
    public void Hide() => Visible = false;
}
