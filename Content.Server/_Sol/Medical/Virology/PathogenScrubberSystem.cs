using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Scrubs non-gas airborne pathogen loads from tiles near active vent scrubbers.
/// </summary>
public sealed class PathogenScrubberSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [Dependency] private readonly GridPathogenAtmosphereSystem _gridPathogen = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;
        var dt = (float)UpdateInterval.TotalSeconds;

        var query = EntityQueryEnumerator<GasVentScrubberComponent, TransformComponent>();
        while (query.MoveNext(out var scrubberUid, out var scrubber, out _))
        {
            if (!scrubber.Enabled || !_power.IsPowered(scrubberUid))
                continue;

            var range = scrubber.WideNet ? 2.5f : 1.25f;
            _gridPathogen.ScrubAround(scrubberUid, 4f * dt, range);
        }
    }
}
