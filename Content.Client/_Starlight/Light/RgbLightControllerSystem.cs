using System.Linq;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;

// ReSharper disable CheckNamespace
namespace Content.Client.Light;

public sealed partial class RgbLightControllerSystem
{
    private static bool IsLayerOf(SpriteComponent sprite, Layer layer) =>
        sprite.LayerData.Any(other => ReferenceEquals(other, layer));

    public bool IsModulating(EntityUid uid, Layer layer, int index)
    {
        if (!TryComp<RgbLightControllerComponent>(uid, out var rgb) || !HasComp<PointLightComponent>(uid))
            return false;

        return rgb.Layers?.Contains(index) ?? rgb.AutoOriginalColors.ContainsKey(layer);
    }

    public bool TryGetOriginalColor(EntityUid uid, Layer layer, int index, out Color color)
    {
        color = default;
        if (!TryComp<RgbLightControllerComponent>(uid, out var rgb) || !HasComp<PointLightComponent>(uid))
            return false;

        if (rgb.Layers != null)
            return rgb.Layers.Contains(index) && rgb.OriginalLayerColors?.TryGetValue(index, out color) == true;

        return rgb.AutoOriginalColors.TryGetValue(layer, out color);
    }

    public void ForgetAutoLayer(EntityUid uid, Layer layer)
    {
        if (TryComp<RgbLightControllerComponent>(uid, out var rgb))
            rgb.AutoOriginalColors.Remove(layer);
    }
}
