// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SLBodyComponent : Component
{
    [DataField, AutoNetworkedField] public Vector2 RootOffset = Vector2.Zero;
    [DataField, AutoNetworkedField] public ProtoId<BodyPrefabPrototype>? Prefab = null;
    [DataField, AutoNetworkedField] public bool BodyBuilt = false;
    [DataField, AutoNetworkedField] public EntityUid? RootPartEntity;
    [DataField, AutoNetworkedField] public int RequiredLegs;
    [ViewVariables] internal readonly List<Entity<SLBodyPartComponent>> BodyParts = new();

    [ViewVariables] internal bool BodyPartsDirty = true;
}
