// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SLBodyPartComponent : Component
{
    [DataField] public ProtoId<BodyPartTypePrototype> PartType = default;
    [DataField] public Dictionary<string, ProtoId<BodyPartSocketPrototype>> Sockets = new();

    [DataField] public BodyPartSocket? ParentSocket = null; //Must be set BEFORE adding to parent's container
    [DataField] public EntityUid Parent = EntityUid.Invalid;
    [DataField] public EntityUid Body = EntityUid.Invalid;
    //Cached container reference for container holding all direct children
    [NonSerialized] public BodyPartContainer? Children = null;
}
