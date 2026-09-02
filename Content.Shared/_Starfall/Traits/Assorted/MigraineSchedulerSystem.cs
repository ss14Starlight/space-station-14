using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Shared._Starfall.Traits.Assorted;

public sealed partial class MigraineSchedulerSystem : EntitySystem
{
    [Dependency]
    private IGameTiming _timing = null!;

    [Dependency]
    private StatusEffectsSystem _statusEffects = null!;

    [Dependency]
    private MobStateSystem _mobState = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MigraineSchedulerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MigrainePreventionEffectComponent, StatusEffectAppliedEvent>(OnPreventionApplied);
        SubscribeLocalEvent<MigrainePreventionEffectComponent, StatusEffectEndTimeUpdatedEvent>(OnPreventionUpdated);
    }

    private void OnMapInit(Entity<MigraineSchedulerComponent> entity, ref MapInitEvent args)
    {
        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(entity));

        ScheduleNext(entity, random.NextDouble());
    }

    private void ScheduleNext(Entity<MigraineSchedulerComponent> entity, double randomValue)
    {
        var interval = entity.Comp.MinTimeBetweenIncidents + (entity.Comp.MaxTimeBetweenIncidents - entity.Comp.MinTimeBetweenIncidents) * randomValue;

        entity.Comp.NextIncidentTime = _timing.CurTime + interval;

        DirtyField(entity, entity.Comp, nameof(entity.Comp.NextIncidentTime));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MigraineSchedulerComponent>();

        while (query.MoveNext(out var uid, out var migraines))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (migraines.NextIncidentTime > _timing.CurTime)
                continue;

            var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(uid));

            // If another system caused a migraine, don't overlap it with the chronic trait's scheduled migraine.
            if (_statusEffects.HasStatusEffect(uid, MigraineEffectComponent.Prototype))
            {
                ScheduleNext((uid, migraines), random.NextDouble());
                continue;
            }

            var duration = migraines.MinDurationOfIncident + (migraines.MaxDurationOfIncident - migraines.MinDurationOfIncident) * random.NextDouble();

            _statusEffects.TrySetStatusEffectDuration(uid, MigraineEffectComponent.Prototype, duration);

            var nextInterval = migraines.MinTimeBetweenIncidents + (migraines.MaxTimeBetweenIncidents - migraines.MinTimeBetweenIncidents) * random.NextDouble();

            // begin the next migraine countdown after the current one ends plus a random interval
            migraines.NextIncidentTime = _timing.CurTime + duration + nextInterval;

            DirtyField(uid, migraines, nameof(migraines.NextIncidentTime));
        }
    }

    private void OnPreventionApplied(Entity<MigrainePreventionEffectComponent> effect, ref StatusEffectAppliedEvent args)
    {
        var protectionEnd = effect.Comp.LastEndTime ?? _timing.CurTime;

        UpdateMigrainePrevention(args.Target, protectionEnd, effect.Comp.IncidentDelay, TimeSpan.Zero);
    }

    private void OnPreventionUpdated(Entity<MigrainePreventionEffectComponent> effect, ref StatusEffectEndTimeUpdatedEvent args)
    {
        if (args.EndTime is not { } newEndTime)
            return;

        var addedProtectionTime = TimeSpan.Zero;

        if (effect.Comp.LastEndTime is { } previousEndTime && newEndTime > previousEndTime)
            addedProtectionTime = newEndTime - previousEndTime;

        effect.Comp.LastEndTime = newEndTime;

        UpdateMigrainePrevention(args.Target, newEndTime, effect.Comp.IncidentDelay, addedProtectionTime);
    }

    /// <summary>
    /// Pauses the existing migraine timer for newly added medicine time and ensures
    /// that no scheduled migraine occurs until the protection ends.
    /// </summary>
    private void UpdateMigrainePrevention(Entity<MigraineSchedulerComponent?> entity, TimeSpan protectionEnd, TimeSpan minimumDelay, TimeSpan addedProtectionTime)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        if (addedProtectionTime > TimeSpan.Zero)
            entity.Comp.NextIncidentTime += addedProtectionTime;

         // Regardless of the original timer, guarantee that the migraine remains
         // at least minimumDelay away after the medicine's protection expires.
        var minimumIncidentTime = protectionEnd + minimumDelay;

        if (entity.Comp.NextIncidentTime < minimumIncidentTime)
            entity.Comp.NextIncidentTime = minimumIncidentTime;

        DirtyField(entity, entity.Comp, nameof(entity.Comp.NextIncidentTime));
    }
}
