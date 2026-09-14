using Content.Server.Stunnable;
using Content.Shared.Drowsiness;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared._Starlight.Sleepiness.Components;
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

    private void OnEffectApplied(Entity<DrowsinessStatusEffectComponent> ent, ref StatusEffectAppliedEvent args) =>
        ent.Comp.NextIncidentTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(
            ent.Comp.TimeBetweenIncidents.X, ent.Comp.TimeBetweenIncidents.Y));

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

            var duration = TimeSpan.FromSeconds(_random.NextFloat(
                drowsiness.DurationOfIncident.X, drowsiness.DurationOfIncident.Y));

            drowsiness.NextIncidentTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(
                drowsiness.TimeBetweenIncidents.X, drowsiness.TimeBetweenIncidents.Y)) + duration;

            // Starlight - Start
            if (drowsiness.SleepIncident)
            {
                var target = statusEffect.AppliedTo.Value;
                _statusEffects.TryAddStatusEffectDuration(target, "StatusEffectSleepiness", duration);

                if (_statusEffects.TryGetTime(target, "StatusEffectSleepiness", out var sleepinessEffect) &&
                    TryComp<SleepinessStatusEffectComponent>(sleepinessEffect.EffectEnt, out var sleepiness))
                {
                    sleepiness.SleepImmediately = true;
                    Dirty(sleepinessEffect.EffectEnt, sleepiness);
                }
            }

            if (drowsiness.KnockdownIncident)
                _stunSystem.TryKnockdown(statusEffect.AppliedTo.Value, duration, force: true);
            // Starlight - End
        }
    }
}
