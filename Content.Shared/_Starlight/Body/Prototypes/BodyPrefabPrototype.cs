// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT
using Content.Shared._Starlight.Body.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Prototypes;

[Prototype]
public sealed partial class BodyPrefabPrototype  : IPrototype
{
    public string ID { get; set; } = string.Empty;

    [DataField(required: true)] public BodyPartDef Root = default!;
}

[DataRecord]
public partial record struct BodyPartDef(
    EntProtoId<SLBodyPartComponent> BodyPart,
    Dictionary<BodyPartSocket, BodyPartDef>? SocketedParts = null,
    List<BodyPartDef>? InternalParts = null)
{
    public bool HasSocketedChildren => SocketedParts is { Count: > 0 };
    public bool HasInternalChildren => InternalParts is { Count: > 0 };
};
