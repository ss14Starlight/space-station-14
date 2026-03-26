using Robust.Client.GameObjects;
using Content.Shared._ST.CosmicCult.Components;

namespace Content.Client._ST.CosmicCult;

/// <summary>
/// Visualizer for The Monument of the Cosmic Cult.
/// </summary>
public sealed class MonumentVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MonumentComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(Entity<MonumentComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
        _sprite.LayerMapTryGet(ent.Owner, MonumentVisualLayers.TransformLayer, out var transformLayer, true);
        _sprite.LayerMapTryGet(ent.Owner, MonumentVisualLayers.MonumentLayer, out var baseLayer, true);
        _appearance.TryGetData<bool>(ent, MonumentVisuals.Transforming, out var transforming, args.Component);
        _appearance.TryGetData<bool>(ent, MonumentVisuals.Tier3, out var tier3, args.Component);
        if (!tier3)
            _sprite.LayerSetRsiState(ent.Owner, transformLayer, "transform-stage2");
        else
            _sprite.LayerSetRsiState(ent.Owner, transformLayer, "transform-stage3");
        if (transforming && HasComp<MonumentTransformingComponent>(ent))
        {
            _sprite.LayerSetAnimationTime(ent.Owner, transformLayer, 0f);
            _sprite.LayerSetVisible(ent.Owner, transformLayer, true);
            _sprite.LayerSetVisible(ent.Owner, baseLayer, false);
        }
        else
        {
            _sprite.LayerSetVisible(ent.Owner, transformLayer, false);
            _sprite.LayerSetVisible(ent.Owner, baseLayer, true);
        }
    }
}
