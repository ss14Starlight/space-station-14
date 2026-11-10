using Content.Shared._Starlight.Breadcrumb.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Breadcrumb.Systems;

/// <summary>
/// Applies the set color to breadcrumbs.
/// </summary>
public sealed class BreadcrumbVisualizer : VisualizerSystem<BreadcrumbComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, BreadcrumbComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        if (!AppearanceSystem.TryGetData(uid, BreadcrumbVisualData.Color, out Color color))
            return;

        SpriteSystem.LayerSetColor(uid, BreadcrumbVisualLayers.UnlitColor, color);
        SpriteSystem.LayerSetColor(uid, BreadcrumbVisualLayers.LitColor, color);
    }
}
