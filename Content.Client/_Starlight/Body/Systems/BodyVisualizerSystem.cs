// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Starlight.Utility;
using Content.Server.Administration.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Client._Starlight.Sprite;

namespace Content.Client._Starlight.Body.Systems;

public sealed class BodyVisualizerSystem : SharedBodyVisualizerSystem
{
    [Dependency] private readonly VisualLayerSystem _visualLayer = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly StarlightEntitySystem _sl = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyVisualizerComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, BodyVisualizerComponent component, ref ComponentHandleState args)
    {
        switch (args.Current)
        {
            case BodyVisualizerFullState full:
                foreach (var key in component.LayerData.Keys)
                    HideLayer(uid, component, key);
                component.Offset = full.Offset;
                component.LayerData.Clear();
                component.LayerKeys.Clear();
                foreach (var (key, value) in full.LayerData)
                {
                    component.LayerData[key] = value;
                    component.LayerKeys.Add(key);
                    ApplyLayerToSprite(uid, component, key, value);
                }
                _visualLayer.ReorderSpriteLayers(uid, component);
                break;

            case BodyVisualizerDeltaState delta:
                component.Offset = delta.Offset;

                if (component.LayerData.Count != delta.AllLayers.Count
                    || !AllPresent(component.LayerData, delta.AllLayers))
                {
                    List<ProtoId<VisualLayerPrototype>>? toRemove = null;
                    foreach (var key in component.LayerData.Keys)
                    {
                        if (!delta.AllLayers.Contains(key))
                            (toRemove ??= []).Add(key);
                    }

                    if (toRemove != null)
                    {
                        foreach (var key in toRemove)
                        {
                            HideLayer(uid, component, key);
                            component.LayerData.Remove(key);
                            component.LayerKeys.Remove(key);
                        }
                    }
                }

                foreach (var (key, value) in delta.ModifiedLayers)
                {
                    component.LayerData[key] = value;
                    component.LayerKeys.Add(key);
                    ApplyLayerToSprite(uid, component, key, value);
                }

                if (delta.ModifiedLayers.Count > 0 || delta.AllLayers.Count != component.LayerData.Count)
                    _visualLayer.ReorderSpriteLayers(uid, component);
                break;
        }
    }

    private void HideLayer(EntityUid uid, BodyVisualizerComponent component, ProtoId<VisualLayerPrototype> layerId)
    {
        var ent = _sl.Entity<SpriteComponent>(uid);
        if (ent.Comp == null)
            return;

        if (_sprite.LayerMapTryGet(ent, layerId, out var index, false))
            _sprite.LayerSetVisible(ent, index, false);
    }

    private void ApplyLayerToSprite(EntityUid uid, BodyVisualizerComponent component, ProtoId<VisualLayerPrototype> layerId, ExtendedSpriteSpecifier specifier)
    {
        var ent = _sl.Entity<SpriteComponent>(uid);
        if (ent.Comp == null)
            return;

        if (!_sprite.LayerMapTryGet(ent, layerId, out var index, false))
            index = _sprite.LayerMapReserve(ent, layerId);

        _sprite.LayerSetSprite(ent, index, specifier.Sprite);
        _sprite.LayerSetColor(ent, index, specifier.SpriteColor);
        _sprite.LayerSetScale(ent, index, specifier.SpriteScale);
    }

    private static bool AllPresent(
        Dictionary<ProtoId<VisualLayerPrototype>, ExtendedSpriteSpecifier> dict,
        HashSet<ProtoId<VisualLayerPrototype>> set)
    {
        foreach (var key in dict.Keys)
        {
            if (!set.Contains(key))
                return false;
        }
        return true;
    }
}
