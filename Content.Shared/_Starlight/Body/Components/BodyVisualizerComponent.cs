// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Utility;
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

