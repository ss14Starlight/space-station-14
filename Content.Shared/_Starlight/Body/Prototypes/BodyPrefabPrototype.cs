// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT
using Content.Shared._Starlight.Body.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Prototypes;

[Prototype]
public sealed partial class BodyPrefabPrototype  : IPrototype
{
    [IdDataField] public string ID { get; set; } = string.Empty;

    [DataField(required: true)] public BodyPartDef Root = default!;
}

[DataRecord]
public partial record struct BodyPartDef(
    EntProtoId<SLBodyPartComponent> BodyPart,
    Dictionary<string, BodyPartDef>? AttachedParts = null,
    List<BodyPartDef>? ContainedParts = null,
    List<EntProtoId<SLBodyPartComponent>>? Alternatives = null)
{
    public bool HasSocketedChildren => AttachedParts is { Count: > 0 };
    public bool HasInternalChildren => ContainedParts is { Count: > 0 };
    public bool HasAlternatives => Alternatives is { Count: > 0 };
};
