// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Preferences;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyPreviewControl
{
    private void AddMarkingLayers(
        BodyPartPreference? pref,
        BodyPartAddress path,
        BodyPartAddress? filterPath,
        MarkingCategories? filterCategory,
        MarkingPrototype? extraMarking,
        HashSet<ProtoId<VisualLayerPrototype>> entityLayerTypes,
        HashSet<VisualLayerKey> partLayers,
        HashSet<ProtoId<ColorAppearanceParameterPrototype>> colorSources,
        List<PreviewLayer> previewLayers)
    {
        var pathMatchesFilter = filterPath != null && path == filterPath.Value;

        if (pref != null)
            AddProfileMarkingLayers(pref, path, pathMatchesFilter, filterCategory, entityLayerTypes, partLayers, colorSources, previewLayers);

        if (pathMatchesFilter && extraMarking != null)
            AddExtraMarkingLayers(extraMarking, path, entityLayerTypes, partLayers, colorSources, previewLayers);
    }

    private void AddProfileMarkingLayers(
        BodyPartPreference pref,
        BodyPartAddress path,
        bool pathMatchesFilter,
        MarkingCategories? filterCategory,
        HashSet<ProtoId<VisualLayerPrototype>> entityLayerTypes,
        HashSet<VisualLayerKey> partLayers,
        HashSet<ProtoId<ColorAppearanceParameterPrototype>> colorSources,
        List<PreviewLayer> previewLayers)
    {
        foreach (var marking in pref.Markings)
        {
            if (!_markingManager!.TryGetMarking(marking, out var markingProto))
                continue;

            if (pathMatchesFilter && filterCategory.HasValue && markingProto.MarkingCategory == filterCategory.Value)
                continue;

            var i = 0;
            foreach (var (layerKey, bodySprite) in markingProto.Sprites)
            {
                if (ShouldSkipMarkingLayer(entityLayerTypes, partLayers, layerKey))
                {
                    i++;
                    continue;
                }

                var color = i < marking.MarkingColors.Count ? marking.MarkingColors[i] : Color.White;
                var colorSource = ResolveMarkingColorSource(bodySprite.ColorSource, marking.MarkingId, layerKey, colorSources);
                previewLayers.Add(new PreviewLayer(new VisualLayerKey(layerKey.Layer), bodySprite.Sprite, color, colorSource, path, Clickable: true, IsMarking: true, MarkingId: marking.MarkingId));
                i++;
            }
        }
    }

    private void AddExtraMarkingLayers(
        MarkingPrototype extraMarking,
        BodyPartAddress path,
        HashSet<ProtoId<VisualLayerPrototype>> entityLayerTypes,
        HashSet<VisualLayerKey> partLayers,
        HashSet<ProtoId<ColorAppearanceParameterPrototype>> colorSources,
        List<PreviewLayer> previewLayers)
    {
        foreach (var (layerKey, bodySprite) in extraMarking.Sprites)
        {
            if (ShouldSkipMarkingLayer(entityLayerTypes, partLayers, layerKey))
                continue;

            var colorSource = ResolveMarkingColorSource(bodySprite.ColorSource, extraMarking.ID, layerKey, colorSources);
            previewLayers.Add(new PreviewLayer(new VisualLayerKey(layerKey.Layer), bodySprite.Sprite, Color.White, colorSource, path, Clickable: false, IsMarking: true, MarkingId: extraMarking.ID));
        }
    }

    private static bool ShouldSkipMarkingLayer(HashSet<ProtoId<VisualLayerPrototype>> entityLayerTypes, HashSet<VisualLayerKey> partLayers, VisualLayerKey layerKey)
        => entityLayerTypes.Contains(layerKey.Layer)
            && !partLayers.Contains(layerKey)
            && !partLayers.Contains(new VisualLayerKey(layerKey.Layer));

    private ProtoId<ColorAppearanceParameterPrototype>? ResolveMarkingColorSource(
        ProtoId<ColorAppearanceParameterPrototype>? source,
        string markingId,
        VisualLayerKey layerKey,
        HashSet<ProtoId<ColorAppearanceParameterPrototype>> colorSources)
    {
        if (source is not { } colorSource)
            return null;

        var perInstance = ResolvePerInstanceColorKey(colorSource, markingId, layerKey);
        colorSources.Add(perInstance);
        return perInstance;
    }
}
