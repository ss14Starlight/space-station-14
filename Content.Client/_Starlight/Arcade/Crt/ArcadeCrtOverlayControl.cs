using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client._Starlight.Arcade.Crt;

/// <summary>
/// Window-scoped faux CRT glass: scanlines and a light phosphor wash.
/// Attach as the last stacking child of a window ContentsContainer so it covers board and menus.
/// Used by Starlight Lancer; available for other arcade UIs if desired.
/// </summary>
public sealed class ArcadeCrtOverlayControl : Control
{
    /// <summary>Pixel spacing between scanlines at UIScale 1.</summary>
    private const float ScanlineSpacing = 6f;

    /// <summary>Alpha of each scanline strip.</summary>
    private const float ScanlineAlpha = 0.10f;

    /// <summary>Matches ArcadeBase PointLight green.</summary>
    private static readonly Color PhosphorColor = Color.FromHex("#3db83b").WithAlpha(0.012f);

    private static readonly Color ScanlineColor = Color.Black.WithAlpha(ScanlineAlpha);

    public ArcadeCrtOverlayControl()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
        AlwaysRender = true;
    }

    /// <summary>
    /// Adds a CRT overlay as the last child of <paramref name="parent"/> if one is not already present.
    /// </summary>
    public static ArcadeCrtOverlayControl Attach(Control parent)
    {
        foreach (var child in parent.Children)
        {
            if (child is ArcadeCrtOverlayControl existing)
            {
                existing.SetPositionLast();
                return existing;
            }
        }

        var overlay = new ArcadeCrtOverlayControl();
        parent.AddChild(overlay);
        return overlay;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        var width = size.X;
        var height = size.Y;

        // Phosphor wash over the whole content area.
        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, size), PhosphorColor);

        // Horizontal scanlines.
        var spacing = Math.Max(1f, ScanlineSpacing * UIScale);
        for (var y = 0f; y < height; y += spacing)
        {
            handle.DrawRect(
                UIBox2.FromDimensions(new Vector2(0f, y), new Vector2(width, Math.Max(1f, UIScale))),
                ScanlineColor);
        }
    }
}
