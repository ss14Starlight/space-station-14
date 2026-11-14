using Content.Shared._Starlight.StationRadio;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.StationRadio.Systems;
using Content.Shared.Power;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.StationRadio.Systems;

public sealed class VinylPlayerSystem : SharedVinylPlayerSystem
{
}

public sealed class VinylPlayerVisualizerSystem : VisualizerSystem<VinylPlayerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, VinylPlayerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        Entity<SpriteComponent?> ent = (uid, sprite);
        AppearanceSystem.TryGetData<bool>(uid, VinylPlayerVisuals.Disc, out var hasDisc, args.Component);

        if (SpriteSystem.LayerMapTryGet(ent, VinylPlayerVisualLayers.Disc, out var discLayer, false))
            SpriteSystem.LayerSetVisible(ent, discLayer, hasDisc);

        if (SpriteSystem.LayerMapTryGet(ent, VinylPlayerVisualLayers.Label, out var labelLayer, false))
        {
            SpriteSystem.LayerSetVisible(ent, labelLayer, hasDisc);
            if (AppearanceSystem.TryGetData<Color>(uid, VinylPlayerVisuals.LabelColor, out var color, args.Component))
                SpriteSystem.LayerSetColor(ent, labelLayer, color);
        }

        AppearanceSystem.TryGetData<int>(uid, VinylPlayerVisuals.SlotMask, out var slotMask, args.Component);
        for (var i = 0; i < component.Slots.Length; i++)
        {
            if (SpriteSystem.LayerMapTryGet(ent, $"slot{i}", out var slotLayer, false))
                SpriteSystem.LayerSetVisible(ent, slotLayer, (slotMask & (1 << i)) != 0);
        }

        if (SpriteSystem.LayerMapTryGet(ent, VinylPlayerVisualLayers.Display, out var displayLayer, false))
        {
            AppearanceSystem.TryGetData<bool>(uid, PowerDeviceVisuals.Powered, out var powered, args.Component);
            var state = hasDisc ? component.DisplayState : component.DisplayEmptyState;
            SpriteSystem.LayerSetVisible(ent, displayLayer, powered && state != null);
            if (state != null)
                SpriteSystem.LayerSetRsiState(ent, displayLayer, state);
        }
    }
}

public sealed class DiscCoverVisualizerSystem : VisualizerSystem<DiscCoverComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DiscCoverComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        Entity<SpriteComponent?> ent = (uid, sprite);
        AppearanceSystem.TryGetData<bool>(uid, DiscCoverVisuals.Disc, out var hasDisc, args.Component);
        AppearanceSystem.TryGetData<int>(uid, DiscCoverVisuals.Pattern, out var pattern, args.Component);
        AppearanceSystem.TryGetData<Color>(uid, DiscCoverVisuals.Main, out var main, args.Component);
        AppearanceSystem.TryGetData<Color>(uid, DiscCoverVisuals.Accent, out var accent, args.Component);

        if (SpriteSystem.LayerMapTryGet(ent, DiscCoverVisualLayers.Base, out var baseLayer, false))
        {
            SpriteSystem.LayerSetVisible(ent, baseLayer, hasDisc || component.ShowEmptyBase);
            SpriteSystem.LayerSetColor(ent, baseLayer, hasDisc ? main : component.EmptyColor);
        }

        if (SpriteSystem.LayerMapTryGet(ent, DiscCoverVisualLayers.Motif, out var motifLayer, false))
        {
            SpriteSystem.LayerSetVisible(ent, motifLayer, hasDisc);
            if (hasDisc)
            {
                SpriteSystem.LayerSetRsiState(ent, motifLayer, $"{component.MotifPrefix}{pattern}");
                SpriteSystem.LayerSetColor(ent, motifLayer, accent);
            }
        }

        if (SpriteSystem.LayerMapTryGet(ent, DiscCoverVisualLayers.Peek, out var peekLayer, false))
            SpriteSystem.LayerSetVisible(ent, peekLayer, hasDisc);
    }
}

public sealed class DiscSpinesVisualizerSystem : VisualizerSystem<DiscSpinesComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DiscSpinesComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        Entity<SpriteComponent?> ent = (uid, sprite);
        AppearanceSystem.TryGetData<DiscSpinesData>(uid, DiscSpinesVisuals.Spines, out var data, args.Component);
        var spines = data?.Colors ?? [];

        for (var i = 0; i < component.Count; i++)
        {
            if (!SpriteSystem.LayerMapTryGet(ent, $"spine{i}", out var layer, false))
                continue;

            var filled = i < spines.Length;
            SpriteSystem.LayerSetVisible(ent, layer, filled);
            if (filled)
                SpriteSystem.LayerSetColor(ent, layer, spines[i]);
        }
    }
}
