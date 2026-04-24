// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Starlight.Utility;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyPartVisualizerComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ExtendedSpriteSpecifier Sprite;
    [DataField, AutoNetworkedField] public ProtoId<VisualLayerPrototype>? BodyVisualLayer = null;
    [DataField, AutoNetworkedField] public Vector2 Offset = Vector2.Zero;
}
