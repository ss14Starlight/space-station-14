using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Moffstation.GameObjects;

/// This <see cref="VisualizerSystem{T}"/> extension helps to implement dynamic visuals with lots of added layers by
/// managing the "lifecycle" of the layers for implementors.
public abstract class ManagedLayerVisualizerSystem<TComp> : VisualizerSystem<TComp> where TComp : Component
{
    private static readonly string LayerPrefix = $"{typeof(TComp).Name}-ManagedLayer-";

    protected override void OnAppearanceChange(EntityUid uid, TComp component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        ref var layersAdded = ref GetSpriteLayersAdded(component);

        // Obliterate existing layers
        var sprite = new Entity<SpriteComponent?>(uid, args.Sprite);
        foreach (var layerAdded in layersAdded)
        {
            SpriteSystem.RemoveLayer(sprite, layerAdded);
        }

        layersAdded.Clear();

        var addedLayers = new HashSet<string>();
        AddLayersOnAppearanceChange(
            component,
            sprite,
            args.Component,
            (partialLayerName, layerData) =>
            {
                var newLayerKey = LayerPrefix + partialLayerName;
                var newLayerIndex = SpriteSystem.AddLayer(sprite, layerData, null);
                SpriteSystem.LayerMapAdd(sprite, newLayerKey, newLayerIndex);
                addedLayers.Add(newLayerKey);
                // ReSharper disable once RedundantAssignment // It's used by a debug assert, you piece.
                var gotLayer = SpriteSystem.TryGetLayer(sprite, newLayerIndex, out var layer, logMissing: true);
                DebugTools.Assert(gotLayer);
                return layer ?? SpriteSystem.AddBlankLayer((sprite, sprite.Comp!));
            }
        );
        layersAdded.UnionWith(addedLayers);
    }

    /// Retrieves a reference to the mutable set of layers added by this visualizer.
    protected abstract ref HashSet<string> GetSpriteLayersAdded(TComp component);

    /// Analogous to <see cref="OnAppearanceChange"/> for standard <see cref="VisualizerSystem{T}"/>s, this function is
    /// called when an <see cref="AppearanceChangeEvent"/> is raised on an entity with a <see cref="SpriteComponent"/>
    /// and <typeparamref name="TComp"/>. Implementors should not directly add layers to <paramref name="sprite"/>, and
    /// should instead use <paramref name="layerFactory"/> to "instantiate" layers from <see cref="PrototypeLayerData"/>.
    protected abstract void AddLayersOnAppearanceChange(
        TComp component,
        Entity<SpriteComponent?> sprite,
        AppearanceComponent appearance,
        LayerFactory layerFactory
    );

    /// The factory function used by <see cref="AddLayersOnAppearanceChange"/>.
    /// <param name="layerKey">A unique string used to identify the layer to create. Used for <see cref="SpriteSystem.LayerMapAdd"/></param>
    /// <param name="layerData">The prototype layer data to use to create the layer.</param>
    /// <returns>The newly created layer, if further modification is desired.</returns>
    protected delegate SpriteComponent.Layer LayerFactory(string layerKey, PrototypeLayerData layerData);
}
