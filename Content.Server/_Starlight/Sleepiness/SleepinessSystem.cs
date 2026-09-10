using Content.Shared.Bed.Sleep;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared._Starlight.Sleepiness.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Sleepiness;

public sealed partial class SleepinessSystem : SharedSleepinessSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SleepinessStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out var uid, out var sleepiness, out var statusEffect))
        {
            if (statusEffect.AppliedTo is not { } target || statusEffect.EndEffectTime is not { } endTime)
                continue;

            var remaining = endTime - _timing.CurTime;

            if (remaining <= sleepiness.RecoveryThreshold)
            {
                if (!sleepiness.SleepTriggered)
                    continue;

                sleepiness.SleepTriggered = false;
                Dirty(uid, sleepiness);
                continue;
            }

            if (remaining < sleepiness.SleepThreshold || sleepiness.SleepTriggered)
                continue;

            if (!_sleeping.TrySleeping(target))
                continue;

            sleepiness.SleepTriggered = true;
            Dirty(uid, sleepiness);
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SleepinessStatusEffectComponent, StatusEffectRemovedEvent>(OnSleepinessRemoved);
    }

    private void OnSleepinessRemoved(Entity<SleepinessStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!ent.Comp.SleepTriggered || !TryComp<SleepingComponent>(args.Target, out var sleeping))
            return;

        _sleeping.TryWaking((args.Target, sleeping));
    }
}
