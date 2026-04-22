// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Collections;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SLBodyComponent : Component
{
    [DataField, AutoNetworkedField] public Vector2 RootOffset = Vector2.Zero;
    [DataField, AutoNetworkedField] public ProtoId<BodyPrefabPrototype>? Prefab = null;
    [DataField, AutoNetworkedField] public bool BodyBuilt = false;
    [DataField, AutoNetworkedField] public EntityUid RootPartEntity = default;

    //Cached list of all body parts for quick enumeration
    [NonSerialized] public ValueList<Entity<SLBodyPartComponent>> BodyParts = new();
}
