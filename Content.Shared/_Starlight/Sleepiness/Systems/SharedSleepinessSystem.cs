using Content.Shared.Bed.Sleep;
using Content.Shared._Starlight.Sleepiness.Components;

namespace Content.Shared._Starlight.Sleepiness.Systems;

public abstract partial class SharedSleepinessSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SleepingComponent, ComponentShutdown>(OnSleepingShutdown);
    }

    private void OnSleepingShutdown(EntityUid uid, SleepingComponent component, ComponentShutdown args) =>
        RemoveSleepCauseMarkers(uid);

    private void RemoveSleepCauseMarkers(EntityUid uid)
    {
        RemComp<SleepinessInducedSleepComponent>(uid);
        RemComp<PlayerRequestedSleepComponent>(uid);
    }
}
