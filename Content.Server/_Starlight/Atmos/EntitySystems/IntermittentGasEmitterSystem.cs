
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Atmos;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Atmos;

public sealed partial class IntermittentGasEmitterSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;

    private Stopwatch stopwatch = new();
    private TimeSpan updateBudget = TimeSpan.FromMilliseconds(0.5);

    public override void Initialize()
    {
        base.Initialize();

        stopwatch.Start();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        stopwatch.Restart();

        var query = EntityQueryEnumerator<IntermittentGasEmitterComponent>();
        while (query.MoveNext(out var uid, out var comp) && stopwatch.Elapsed < updateBudget)
        {
            if (comp.LastEmit + comp.EmitPeriod > _timing.CurTime) return;
            comp.LastEmit = _timing.CurTime;

            var mixture = _atmos.GetContainingMixture(uid, false, true) ?? new();
            mixture.AdjustMoles(comp.GasType, comp.Moles);
        }
    }
}
