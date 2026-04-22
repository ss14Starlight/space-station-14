// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Starlight.Utility;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SLBodyPartComponent : Component
{
    [DataField, AutoNetworkedField] public ProtoId<BodyPartTypePrototype> PartType = default;
    [DataField, AutoNetworkedField] public Dictionary<string, ProtoId<BodyPartSocketPrototype>> Sockets = new();
    [DataField, AutoNetworkedField] public BodyPartSocket? ParentSocket = null; //Must be set BEFORE adding to parent's container
    [DataField, AutoNetworkedField] public EntityUid Parent = EntityUid.Invalid;
    [DataField, AutoNetworkedField] public EntityUid Body = EntityUid.Invalid;
    //Cached container reference for container holding all direct children
    [NonSerialized] public BodyPartContainer? Children = null;
}
