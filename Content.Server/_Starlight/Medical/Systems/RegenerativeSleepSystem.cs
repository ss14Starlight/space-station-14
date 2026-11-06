using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Systems;
using Content.Shared._Starlight.Medical.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Systems;

public sealed partial class RegenerativeSleepSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<RegenerativeSleepComponent>();
        while (query.MoveNext(out var uid, out var regenerativeSleep))
        {
            if (!HasComp<SleepingComponent>(uid))
            {
                regenerativeSleep.NextHeal = curTime;
                continue;
            }

            if (regenerativeSleep.NextHeal > curTime)
                continue;

            _damageable.TryChangeDamage(uid, -regenerativeSleep.HealingPerUpdate, true, origin: uid);
            _bloodstream.TryModifyBleedAmount(uid, -regenerativeSleep.BleedHealPerUpdate);
            regenerativeSleep.NextHeal = curTime + regenerativeSleep.UpdateInterval;
        }
    }
}
