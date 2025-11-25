using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Access;

[Serializable, NetSerializable]
public sealed class IdCardAccessUpdatedEvent : EntityEventArgs
{
    public NetEntity TargetId;
    public List<ProtoId<AccessLevelPrototype>> OldAccesses = [];
    public List<ProtoId<AccessLevelPrototype>> NewAccesses = [];
}