// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Starlight.Utility;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SLBodyPartComponent : Component
{
    [DataField] public ProtoId<BodyPartTypePrototype> PartType = default;
    [DataField] public ExtendedSpriteSpecifier? ExternalSprite = null;
    [DataField] public Dictionary<string, ProtoId<BodyPartSocketPrototype>> Sockets = new();

    [DataField] public BodyPartSocket? ParentSocket = null; //Must be set BEFORE adding to parent's container
    [DataField] public EntityUid Parent = EntityUid.Invalid;
    [DataField] public EntityUid Body = EntityUid.Invalid;
    //Cached container reference for container holding all direct children
    [NonSerialized] public BodyPartContainer? Children = null;
}
