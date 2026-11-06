using Content.Shared._Starlight.StationRadio.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.StationRadio.Systems;

public abstract partial class SharedBroadcastRecorderSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _sharedAppearance = default!;
    [Dependency] private IGameTiming _sharedTiming = default!;

    [SubscribeLocalEvent]
    private void OnTapeInserted(EntityUid uid, BroadcastRecorderComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.Slot || _sharedTiming.ApplyingState)
            return;

        _sharedAppearance.SetData(uid, BroadcastRecorderVisuals.Tape, true);
        TapeInserted((uid, comp));
    }

    [SubscribeLocalEvent]
    private void OnTapeRemoved(EntityUid uid, BroadcastRecorderComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.Slot || _sharedTiming.ApplyingState)
            return;

        _sharedAppearance.SetData(uid, BroadcastRecorderVisuals.Tape, false);
        TapeRemoved((uid, comp), args.Entity);
    }

    protected virtual void TapeInserted(Entity<BroadcastRecorderComponent> ent)
    {
    }

    protected virtual void TapeRemoved(Entity<BroadcastRecorderComponent> ent, EntityUid tape)
    {
    }
}
