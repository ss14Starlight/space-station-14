// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Body.Prototypes;
[Prototype]
public sealed partial class BodyPartSocketPrototype : IPrototype
{
    public string ID { get; set; } = string.Empty;
    public bool HasRestrictions => AllowedTypes != null;
    [DataField] public HashSet<ProtoId<BodyPartTypePrototype>>? AllowedTypes = null;
}
[DataRecord, NetSerializable]
public partial record struct BodyPartSocket(
    string SocketId,
    ProtoId<BodyPartTypePrototype> SocketType,
    HashSet<ProtoId<BodyPartTypePrototype>>? AllowedPartTypes);
