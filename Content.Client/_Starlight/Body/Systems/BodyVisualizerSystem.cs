// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Utility;
using Content.Shared._Starlight;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Content.Client._Starlight.Sprite;

namespace Content.Client._Starlight.Body.Systems;

public sealed partial class BodyVisualizerSystem : SharedBodyVisualizerSystem
{
    [Dependency] private VisualLayerSystem _visualLayer = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private StarlightEntitySystem _sl = default!;

    [SubscribeLocalEvent]
    private void OnHandleState(EntityUid uid, BodyVisualizerComponent component, ref ComponentHandleState args)
    {
        switch (args.Current)
        {
            case BodyVisualizerFullState full:
                foreach (var key in component.LayerData.Keys)
                    HideLayer(uid, key);
                component.Offset = full.Offset;
                component.LayerData.Clear();
                component.LayerKeys.Clear();
                foreach (var (key, value) in full.LayerData)
                {
                    component.LayerData[key] = value;
                    component.LayerKeys.Add(key);
                    ApplyLayerToSprite(uid, key, value);
                }
                _visualLayer.ReorderSpriteLayers(uid, component);
                break;

        }
    }

    private void HideLayer(EntityUid uid, VisualLayerKey layerId)
    {
        var ent = _sl.Entity<SpriteComponent>(uid);
        if (ent.Comp == null)
            return;

        var keyStr = layerId.ToString();
        if (_sprite.LayerMapTryGet(ent, keyStr, out var index, false))
            _sprite.LayerSetVisible(ent, index, false);
    }

    private void ApplyLayerToSprite(EntityUid uid, VisualLayerKey layerId, ExtendedSpriteSpecifier specifier)
    {
        var ent = _sl.Entity<SpriteComponent>(uid);
        if (ent.Comp == null)
            return;

        var keyStr = layerId.ToString();
        if (!_sprite.LayerMapTryGet(ent, keyStr, out var index, false))
            index = _sprite.LayerMapReserve(ent, keyStr);

        _sprite.LayerSetSprite(ent, index, specifier.Sprite);
        _sprite.LayerSetColor(ent, index, specifier.SpriteColor);
        _sprite.LayerSetScale(ent, index, specifier.SpriteScale);

        if (!layerId.Displacement)
            return;

        var originalKey = new VisualLayerKey(layerId.Layer, layerId.Index);
        var originalKeyStr = originalKey.ToString();
        if (!_sprite.LayerMapTryGet(ent, originalKeyStr, out var originalIndex, false))
            originalIndex = _sprite.LayerMapReserve(ent, originalKeyStr);

        ent.Comp.LayerSetShader(originalIndex, "DisplacedDraw");

        if (!_sprite.TryGetLayer(ent, index, out var layer, true))
            return;

        layer.CopyToShaderParameters ??= new SpriteComponent.CopyToShaderParameters(
            originalKeyStr);
        layer.CopyToShaderParameters!.ParameterTexture = "displacementMap";
        layer.CopyToShaderParameters!.ParameterUV = "displacementUV";
    }
}
