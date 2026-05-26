// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Starlight.Utility;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedBodyVisualizerSystem))]
public sealed partial class BodyVisualizerComponent : Component
{
    [DataField] public Vector2 Offset = Vector2.Zero;

    [DataField] public BodyProfile? Appearance;

    [DataField] public Dictionary<VisualLayerKey, ExtendedSpriteSpecifier> LayerData = [];

    /// <summary>
    /// Tracks the last game tick each layer key was modified, for delta state networking.
    /// Removed keys are detected by their absence from <see cref="LayerData"/> while still
    /// present in the client's full state (the delta carries the authoritative key set).
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<VisualLayerKey, GameTick> LayerModifiedTicks = [];

    /// <summary>
    /// Mirror of <see cref="LayerData"/>'s key set. Reused as the authoritative key set
    /// in delta states to avoid per-tick allocations. Server-only; the client maintains
    /// its own copy implicitly through <see cref="LayerData"/>.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<VisualLayerKey> LayerKeys = [];
}

[Serializable, NetSerializable]
public sealed class BodyVisualizerFullState(
    Vector2 offset,
    Dictionary<VisualLayerKey, ExtendedSpriteSpecifier> layerData)
    : ComponentState
{
    public Vector2 Offset = offset;
    public Dictionary<VisualLayerKey, ExtendedSpriteSpecifier> LayerData = layerData;
}

[Serializable, NetSerializable]
public sealed class BodyVisualizerDeltaState(
    Vector2 offset,
    Dictionary<VisualLayerKey, ExtendedSpriteSpecifier> modifiedLayers,
    HashSet<VisualLayerKey> allLayers)
    : ComponentState, IComponentDeltaState<BodyVisualizerFullState>
{
    public Vector2 Offset = offset;
    public Dictionary<VisualLayerKey, ExtendedSpriteSpecifier> ModifiedLayers = modifiedLayers;
    public HashSet<VisualLayerKey> AllLayers = allLayers;

    public void ApplyToFullState(BodyVisualizerFullState state)
    {
        state.Offset = Offset;

        if (state.LayerData.Count != AllLayers.Count || !AllPresent(state.LayerData, AllLayers))
        {
            List<VisualLayerKey>? toRemove = null;
            foreach (var key in state.LayerData.Keys)
            {
                if (!AllLayers.Contains(key))
                    (toRemove ??= []).Add(key);
            }

            if (toRemove != null)
            {
                foreach (var key in toRemove)
                    state.LayerData.Remove(key);
            }
        }

        foreach (var (key, value) in ModifiedLayers)
            state.LayerData[key] = value;
    }

    public BodyVisualizerFullState CreateNewFullState(BodyVisualizerFullState state)
    {
        var layers = new Dictionary<VisualLayerKey, ExtendedSpriteSpecifier>(AllLayers.Count);

        foreach (var (key, value) in state.LayerData)
        {
            if (AllLayers.Contains(key))
                layers[key] = value;
        }

        foreach (var (key, value) in ModifiedLayers)
            layers[key] = value;

        return new BodyVisualizerFullState(Offset, layers);
    }

    private static bool AllPresent(
        Dictionary<VisualLayerKey, ExtendedSpriteSpecifier> dict,
        HashSet<VisualLayerKey> set)
    {
        foreach (var key in dict.Keys)
        {
            if (!set.Contains(key))
                return false;
        }
        return true;
    }
}
