using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;

namespace Content.Shared._Impstation.Replicator;

public sealed class SharedReplicatorSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMindAdded(Entity<ReplicatorComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.HasSpawnedNest)
            return;

        if (!ent.Comp.Queen) // if you're the queen, which you'll only be if you're the first one spawned,
            return;

        _actions.AddAction(ent, ent.Comp.SpawnNewNestAction);

        ent.Comp.HasSpawnedNest = true;
    }
}
