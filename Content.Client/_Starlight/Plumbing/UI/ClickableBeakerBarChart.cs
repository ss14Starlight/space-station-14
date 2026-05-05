using Content.Client.Medical.Cryogenics;
using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.Palette;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Plumbing.UI;

public sealed class ClickableBeakerBarChart : ContainerButton
{
    private const float StateLightnessShift = 0.10f;

    private static readonly ColorPalette ButtonPalette = Palettes.Navy;
    private static readonly Color IdleBackgroundColor = ButtonPalette.Element;
    private static readonly Color ChartBackgroundColor = new(0.1f, 0.1f, 0.1f);
    private static readonly Color HoverBackgroundColor = ButtonPalette.HoveredElement.NudgeLightness(StateLightnessShift);
    private static readonly Color PressedBackgroundColor = ButtonPalette.PressedElement.NudgeLightness(StateLightnessShift);

    private readonly BeakerBarChart _chart;

    public event Action<string>? OnChartPressed;

    public string ReagentId { get; set; } = string.Empty;

    public float Capacity
    {
        get => _chart.Capacity;
        set => _chart.Capacity = value;
    }

    public ClickableBeakerBarChart()
    {
        HorizontalExpand = true;
        MouseFilter = MouseFilterMode.Stop;
        ToolTip = string.Empty;
        OnPressed += _ => HandlePressed();

        var chartContainer = new BoxContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(4),
        };

        _chart = new BeakerBarChart
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            BackgroundColor = ChartBackgroundColor,
        };

        chartContainer.AddChild(_chart);
        AddChild(chartContainer);
        UpdateButtonStyle();
    }

    public void Clear()
    {
        _chart.Clear();
    }

    public void SetEntry(
        string uid,
        string label,
        float amount,
        Color color,
        Color? textColor = null,
        string? tooltip = null)
    {
        ToolTip = tooltip;
        _chart.SetEntry(uid, label, amount, color, textColor, tooltip);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateButtonStyle();
    }

    private void HandlePressed()
    {
        if (string.IsNullOrEmpty(ReagentId))
            return;

        OnChartPressed?.Invoke(ReagentId);
    }

    private void UpdateButtonStyle()
    {
        var backgroundColor = DrawMode switch
        {
            DrawModeEnum.Pressed => PressedBackgroundColor,
            DrawModeEnum.Hover => HoverBackgroundColor,
            _ => IdleBackgroundColor,
        };

        StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = backgroundColor,
            BorderColor = Color.Transparent,
            BorderThickness = new Thickness(0),
            ContentMarginLeftOverride = 0,
            ContentMarginTopOverride = 0,
            ContentMarginRightOverride = 0,
            ContentMarginBottomOverride = 0,
        };
    }
}
