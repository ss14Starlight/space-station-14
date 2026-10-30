using Content.Shared._Starlight.Breadcrumb.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Breadcrumb.Systems;

public sealed class BreadcrumbDroppingVisualizer : VisualizerSystem<BreadcrumbDroppingComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, BreadcrumbDroppingComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        if (AppearanceSystem.TryGetData(uid, BreadcrumbDroppingVisualLayers.Color, out Color color))
            SpriteSystem.LayerSetColor(uid, BreadcrumbDroppingVisualLayers.Color, color);
    }
}
