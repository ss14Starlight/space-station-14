using Content.Shared._Starlight.Breadcrumb.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Breadcrumb.Systems;

/// <summary>
/// Visualizes the open/closed and fill states for breadcrumb droppers.
/// </summary>
public sealed class BreadcrumbDropperVisualizer : VisualizerSystem<BreadcrumbDropperComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, BreadcrumbDropperComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        AppearanceSystem.TryGetData(uid, BreadcrumbDropperVisuals.Enabled, out bool enabled);
        AppearanceSystem.TryGetData(uid, BreadcrumbDropperVisuals.Fill, out BreadcrumbDropperFillLevel level);

        // Only show the fill while the bag is open and actually has stones in it. The open/closed base layer itself
        // is handled by the GenericVisualizer in YAML.
        var state = level switch
        {
            BreadcrumbDropperFillLevel.Low => "fill-1",
            BreadcrumbDropperFillLevel.Medium => "fill-2",
            BreadcrumbDropperFillLevel.High => "fill-3",
            _ => null,
        };

        if (enabled && state != null)
        {
            SpriteSystem.LayerSetRsiState(uid, BreadcrumbDropperVisualLayers.Fill, state);
            SpriteSystem.LayerSetVisible(uid, BreadcrumbDropperVisualLayers.Fill, true);

            if (AppearanceSystem.TryGetData(uid, BreadcrumbDropperVisuals.Color, out Color color))
                SpriteSystem.LayerSetColor(uid, BreadcrumbDropperVisualLayers.Fill, color);
        }
        else
        {
            SpriteSystem.LayerSetVisible(uid, BreadcrumbDropperVisualLayers.Fill, false);
        }
    }
}
