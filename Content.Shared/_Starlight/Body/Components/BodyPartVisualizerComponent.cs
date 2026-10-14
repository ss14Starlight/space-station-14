// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyPartVisualizerComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<VisualLayerKey, BodySpriteSpecifier> BodyVisualLayers = [];

    [DataField, AutoNetworkedField]
    public Dictionary<string, List<VisualLayerKey>> SocketLayers = [];

    [DataField, AutoNetworkedField]
    public List<ProtoId<MarkingSetPrototype>> MarkingSets = [];
}

