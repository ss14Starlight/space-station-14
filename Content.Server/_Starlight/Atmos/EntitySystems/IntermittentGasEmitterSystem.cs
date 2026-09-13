
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Atmos;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Atmos.EntitySystems;

public sealed partial class IntermittentGasEmitterSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IntermittentGasEmitterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.LastEmit + comp.EmitPeriod > _timing.CurTime) continue;
            comp.LastEmit = _timing.CurTime;

            var mixture = _atmos.GetContainingMixture(uid, false, true) ?? new();
            mixture.AdjustMoles(comp.GasType, comp.Moles);
        }
    }
}
