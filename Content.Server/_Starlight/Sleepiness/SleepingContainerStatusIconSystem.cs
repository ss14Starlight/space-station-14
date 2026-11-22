using Content.Shared.Bed.Sleep;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Sleepiness;

public sealed partial class SleepingContainerStatusIconSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnSleepingStartup(EntityUid uid, SleepingComponent component, ComponentStartup args)
    {
        var parent = Transform(uid).ParentUid;
        if (TryComp<ContainerManagerComponent>(parent, out _))
            EnsureComp<StatusIconComponent>(parent);
    }

    [SubscribeLocalEvent]
    private void OnSleepingInserted(EntityUid uid, SleepingComponent component, EntGotInsertedIntoContainerMessage args) =>
        EnsureComp<StatusIconComponent>(args.Container.Owner);
}
