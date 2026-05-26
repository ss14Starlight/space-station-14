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
    private const int MaxBodyTreeDepth = 32;

    private BodyEditorBodyPartState? AddPart(
        BodyPartDef part,
        BodyPartPreference? pref,
        string slotId,
        BodyPartAddress path,
        string? parentSocket,
        int depth,
        List<PreviewLayer> previewLayers,
        BodyPartAddress? filterPath = null,
        MarkingCategories? filterCategory = null,
        MarkingPrototype? extraMarking = null)
    {
        if (_prototype == null || depth > MaxBodyTreeDepth)
            return null;

        var entityProtoId = pref?.BodyPartOverride?.Id ?? part.BodyPart.Id;
        var markingSets = GetMarkingSets(entityProtoId);
        var layers = GetLayers(entityProtoId, parentSocket);
        var entityLayerTypes = GetEntityLayerTypes(entityProtoId);
        var partLayers = new HashSet<VisualLayerKey>();
        var colorSources = new HashSet<ProtoId<ColorAppearanceParameterPrototype>>();
        VisualLayerKey? topLayerKey = null;

        foreach (var (layerId, specifier) in layers)
        {
            partLayers.Add(layerId);
            if (specifier.ColorSource is { } source)
                colorSources.Add(source);
            previewLayers.Add(new PreviewLayer(layerId, specifier.Sprite, specifier.SpriteColor, specifier.ColorSource, path, Clickable: true));
            topLayerKey = layerId;
        }

        if (topLayerKey is { } && _markingManager != null)
            AddMarkingLayers(pref, path, filterPath, filterCategory, extraMarking, entityLayerTypes, partLayers, colorSources, previewLayers);

        var children = new List<BodyEditorBodyPartState>();
        if (part.AttachedParts != null)
        {
            foreach (var (childSlotId, childPart) in part.AttachedParts)
            {
                var childPref = pref?.Children.GetValueOrDefault(childSlotId);
                var childState = AddPart(childPart, childPref, childSlotId, path.Append(childSlotId), GetVisualSocketId(childSlotId, parentSocket), depth + 1, previewLayers, filterPath, filterCategory, extraMarking);
                if (childState != null)
                    children.Add(childState);
            }
        }

        return new BodyEditorBodyPartState
        {
            SlotId = slotId,
            Path = path,
            Layers = partLayers,
            ColorSources = colorSources,
            MarkingSets = markingSets,
            Children = children,
        };
    }

}
