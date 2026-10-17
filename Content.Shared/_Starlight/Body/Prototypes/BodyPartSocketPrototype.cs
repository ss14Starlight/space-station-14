// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Body.Prototypes;
[Prototype]
public sealed partial class BodyPartSocketPrototype : IPrototype
{
    [IdDataField] public string ID { get; set; } = string.Empty;
    public bool HasRestrictions => AllowedTypes != null;
    [DataField] public HashSet<ProtoId<BodyPartTypePrototype>>? AllowedTypes = null;

    /// <summary>
    /// Internal sockets hold parts inside their parent (organs, implants) instead of attaching them to its
    /// outside. Internal parts are not hit directly and take no part in the body's outline.
    /// </summary>
    [DataField] public bool Internal;
}
[DataRecord, Serializable, NetSerializable]
public partial record struct BodyPartSocket(
    string SocketId,
    ProtoId<BodyPartSocketPrototype> SocketType);
