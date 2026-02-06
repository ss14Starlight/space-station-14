// these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

using Content.Shared.Actions;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared._Impstation.Replicator;

[ByRefEvent]
public sealed partial class ReplicatorNestEmbiggenedEvent(Entity<ReplicatorNestComponent> ent) : EntityEventArgs
{
    public Entity<ReplicatorNestComponent> Ent { get; set; } = ent;
}

public sealed partial class ReplicatorSpawnNestActionEvent : InstantActionEvent;

public sealed partial class ReplicatorUpgradeActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> NextStage;
}
