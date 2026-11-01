// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Prototypes;

[Prototype]
public sealed partial class VisualLayerPrototype : IPrototype
{
    [IdDataField] public string ID { get; set; } = string.Empty;
    [DataField] public HashSet<ProtoId<VisualLayerPrototype>>? AboveLayers = null;
    [DataField] public HashSet<ProtoId<VisualLayerPrototype>>? BelowLayers = null;
}
