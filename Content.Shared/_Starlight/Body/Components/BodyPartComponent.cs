// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Utility;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SLBodyPartComponent : Component
{
    [DataField, AutoNetworkedField] public ProtoId<BodyPartTypePrototype> PartType = "Other";

    [DataField, AutoNetworkedField] public BodySide Side = BodySide.None;
    [ViewVariables]
    public BodyPartType Kind => Enum.TryParse<BodyPartType>(PartType.Id, out var kind) ? kind : BodyPartType.Other;

    [ViewVariables]
    public BodyPartSymmetry Symmetry => Side switch
    {
        BodySide.Left => BodyPartSymmetry.Left,
        BodySide.Right => BodyPartSymmetry.Right,
        _ => BodyPartSymmetry.None,
    };
    [DataField, AutoNetworkedField] public Dictionary<string, ProtoId<BodyPartSocketPrototype>> Sockets = new();
    [DataField, AutoNetworkedField] public BodyPartSocket? ParentSocket = null; //Must be set BEFORE adding to parent's container
    [DataField, AutoNetworkedField] public EntityUid? Parent;
    [DataField, AutoNetworkedField] public EntityUid? Body;
    //Cached container reference for container holding all direct children
    [NonSerialized] public Container? Children = null;
}
