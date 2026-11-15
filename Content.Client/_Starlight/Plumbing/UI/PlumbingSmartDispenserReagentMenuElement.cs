using System.Numerics;
using Content.Client._Starlight.Verbs.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Plumbing.UI;

/// <summary>
/// Verb menu entry for one reagent stored in a smart dispenser, with a bar in the reagent's colour
/// showing how full its storage is.
/// </summary>
public sealed class PlumbingSmartDispenserReagentMenuElement : VerbMenuEntryElement
{
    private static readonly Vector2 _barSize = new(8, 24);

    private readonly Func<float> _fill;
    private readonly PanelContainer _fillBar;

    /// <param name="fill">Stored fraction of the per-reagent cap, read every frame while the menu is open.</param>
    /// <param name="tooltip">Built on hover.</param>
    public PlumbingSmartDispenserReagentMenuElement(string text, Color color, Func<float> fill, Func<string> tooltip)
        : base(text)
    {
        _fill = fill;

        TooltipSupplier = _ =>
        {
            var tip = new Tooltip();
            tip.SetMessage(FormattedMessage.FromUnformatted(tooltip()));
            return tip;
        };

        _fillBar = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = color.WithAlpha(1f) },
            VerticalAlignment = VAlignment.Bottom,
        };

        var bar = new Control
        {
            SetSize = _barSize,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        bar.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = color.WithAlpha(0.25f) },
        });
        bar.AddChild(_fillBar);
        Icon.AddChild(bar);

        UpdateFill();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateFill();
    }

    private void UpdateFill()
    {
        var fraction = Math.Clamp(_fill(), 0f, 1f);

        // Full only at the cap; any stock at all stays visible.
        var height = fraction > 0f ? MathF.Max(1f, MathF.Floor(fraction * _barSize.Y)) : 0f;
        if (_fillBar.SetHeight != height)
            _fillBar.SetHeight = height;
    }
}
