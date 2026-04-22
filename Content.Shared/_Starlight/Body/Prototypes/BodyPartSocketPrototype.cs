// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Shared._Starlight.Body.Prototypes;
[Prototype]
public sealed partial class BodyPartSocketPrototype : IPrototype
{
    [IdDataField] public string ID { get; set; } = string.Empty;
    public bool HasRestrictions => AllowedTypes != null;
    [DataField] public HashSet<ProtoId<BodyPartTypePrototype>>? AllowedTypes = null;
}
[DataRecord, Serializable, NetSerializable]
public partial record struct BodyPartSocket(
    string SocketId,
    ProtoId<BodyPartSocketPrototype> SocketType);
