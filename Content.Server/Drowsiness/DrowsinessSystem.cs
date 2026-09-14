using Content.Server.Stunnable;
using Content.Shared.Drowsiness;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Drowsiness;

public sealed partial class DrowsinessSystem : SharedDrowsinessSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private StunSystem _stunSystem = default!; // Starlight

    /// <inheritdoc/>
    public override void Initialize() =>
        SubscribeLocalEvent<DrowsinessStatusEffectComponent, StatusEffectAppliedEvent>(OnEffectApplied);

    private void OnEffectApplied(Entity<DrowsinessStatusEffectComponent> ent, ref StatusEffectAppliedEvent args) => ent.Comp.NextIncidentTime = _timing.CurTime + GetIncidentDelay(ent.Comp);

    private TimeSpan GetIncidentDelay(DrowsinessStatusEffectComponent drowsiness) => TimeSpan.FromSeconds(_random.NextFloat(
        drowsiness.SleepIncident ? drowsiness.SleepinessTimeBetweenIncidents.X : drowsiness.TimeBetweenIncidents.X,
        drowsiness.SleepIncident ? drowsiness.SleepinessTimeBetweenIncidents.Y : drowsiness.TimeBetweenIncidents.Y));

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DrowsinessStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out var uid, out var drowsiness, out var statusEffect))
        {
            if (_timing.CurTime < drowsiness.NextIncidentTime)
                continue;

            if (statusEffect.AppliedTo is null)
                continue;

            // Starlight - Start
            if (drowsiness.SleepIncident)
            {
                drowsiness.NextIncidentTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(
                    drowsiness.SleepinessTimeBetweenIncidents.X, drowsiness.SleepinessTimeBetweenIncidents.Y));

                var sleepiness = TimeSpan.FromSeconds(_random.NextFloat(
                    drowsiness.SleepinessIncrement.X, drowsiness.SleepinessIncrement.Y));
                _statusEffects.TryAddStatusEffectDuration(statusEffect.AppliedTo.Value, "StatusEffectSleepiness", sleepiness);
            }

            if (drowsiness.KnockdownIncident)
            {
                var duration = TimeSpan.FromSeconds(_random.NextFloat(drowsiness.DurationOfIncident.X, drowsiness.DurationOfIncident.Y));
                drowsiness.NextIncidentTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(
                    drowsiness.TimeBetweenIncidents.X, drowsiness.TimeBetweenIncidents.Y)) + duration;
                _stunSystem.TryKnockdown(statusEffect.AppliedTo.Value, duration, force: true);
            }

            if (!drowsiness.SleepIncident && !drowsiness.KnockdownIncident)
                drowsiness.NextIncidentTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(
                    drowsiness.TimeBetweenIncidents.X, drowsiness.TimeBetweenIncidents.Y));
            // Starlight - End
        }
    }
}
