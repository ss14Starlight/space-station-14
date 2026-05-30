// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyPreviewControl
{
    private List<KeyValuePair<VisualLayerKey, BodySpriteSpecifier>> GetLayers(string entityProtoId, string? parentSocket)
    {
        var layers = new List<KeyValuePair<VisualLayerKey, BodySpriteSpecifier>>();

        if (_prototype == null
            || !_prototype.TryIndex<EntityPrototype>(entityProtoId, out var entityPrototype)
            || !entityPrototype.TryGetComponent<BodyPartVisualizerComponent>(out var visualizer, _componentFactory))
            return layers;

        foreach (var layer in visualizer.BodyVisualLayers)
        {
            if (IsLayerAllowed(layer.Key, visualizer, parentSocket))
                layers.Add(layer);
        }

        layers.Sort((a, b) => _visualLayers.CompareLayers(a.Key, b.Key));
        return layers;
    }

    private IReadOnlyList<ProtoId<MarkingSetPrototype>> GetMarkingSets(string entityProtoId)
    {
        if (_prototype == null
            || !_prototype.TryIndex<EntityPrototype>(entityProtoId, out var entityPrototype)
            || !entityPrototype.TryGetComponent<BodyPartVisualizerComponent>(out var visualizer, _componentFactory)
            || visualizer.MarkingSets.Count == 0)
            return [];

        return visualizer.MarkingSets;
    }

    private HashSet<ProtoId<VisualLayerPrototype>> GetEntityLayerTypes(string entityProtoId)
    {
        var set = new HashSet<ProtoId<VisualLayerPrototype>>();
        if (_prototype == null
            || !_prototype.TryIndex<EntityPrototype>(entityProtoId, out var entityPrototype)
            || !entityPrototype.TryGetComponent<BodyPartVisualizerComponent>(out var visualizer, _componentFactory))
            return set;

        foreach (var (key, _) in visualizer.BodyVisualLayers)
            set.Add(key.Layer);
        return set;
    }

    private static bool IsLayerAllowed(VisualLayerKey layerId, BodyPartVisualizerComponent visualizer, string? parentSocket)
    {
        if (visualizer.SocketLayers.Count == 0)
            return true;

        var socketId = parentSocket ?? "root";
        if (!visualizer.SocketLayers.TryGetValue(socketId, out var allowed))
            return false;

        foreach (var allowedKey in allowed)
        {
            if (allowedKey.Layer == layerId.Layer)
                return true;
        }

        return false;
    }
}
