// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Prototypes;

[Prototype]
public sealed partial class MarkingSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    // Stop being lazy, explicitly defined is always better.
    // TODO: rework to <c>EntProtoId</c> once markings are migrated onto entity prototypes.
    [DataField]
    public List<ProtoId<MarkingPrototype>> Markings = [];

    [DataField]
    public Vector2 Offset = Vector2.Zero;

    [DataField]
    public int MinCount;

    [DataField]
    public int MaxCount = 1;
}
