using Content.Shared.Actions;
using Content.Shared.Polymorph;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Impstation.Replicator;

[Serializable, NetSerializable]
public sealed class ReplicatorNestEmbiggenedEvent : EntityEventArgs
{
    public NetEntity Ent { get; set; }

    public ReplicatorNestEmbiggenedEvent(NetEntity ent)
    {
        Ent = ent;
    }
}

public sealed partial class ReplicatorSpawnNestActionEvent : InstantActionEvent;

public sealed partial class ReplicatorUpgradeActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> NextStage;
}
