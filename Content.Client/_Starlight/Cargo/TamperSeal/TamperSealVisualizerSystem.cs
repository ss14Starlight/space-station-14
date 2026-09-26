using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Cargo.TamperSeal;

/// <summary>
/// Visualizes a container that is tamper-sealed.
/// </summary>
public sealed partial class TamperSealVisualizerSystem : VisualizerSystem<TamperSealComponent>
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, TamperSealComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var opened = component.Opened;
        var destroyed = component.Destroyed;
        var color = component.Color;

        var ent = (uid, args.Sprite);
        ShowLayerConditional(ent, TamperSealLayers.Base, color, true);
        ShowLayerConditional(ent, TamperSealLayers.Sealed, color, !opened);
        ShowLayerConditional(ent, TamperSealLayers.Opened, color, opened);
        ShowLayerConditional(ent, TamperSealLayers.Destroyed, color, opened && destroyed);
    }

    /// <summary>
    /// Sets layer visibility and color if it exists.
    /// </summary>
    private void ShowLayerConditional(Entity<SpriteComponent?> sprite, Enum layerKey, Color color, bool value)
    {
        if (_sprite.LayerMapTryGet(sprite, layerKey, out var layer, false))
        {
            _sprite.LayerSetVisible(sprite, layer, value);
            _sprite.LayerSetColor(sprite, layer, color);
        }
    }

}
