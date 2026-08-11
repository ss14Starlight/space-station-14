using Content.Server.Destructible;
using Content.Server._Starlight.CosmicCult.Components;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;

namespace Content.Server._Starlight.CosmicCult;

public sealed class CosmicRiftHealthSystem : EntitySystem
{
    [Dependency] private readonly CosmicMalignEmpoweredRiftSystem _riftSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var corpseCount = _riftSystem.StoredCorpseCount;

        var query = EntityQueryEnumerator<
            CosmicRiftHealthComponent,
            MobThresholdsComponent,
            DamageableComponent,
            DestructibleComponent>();

        while (query.MoveNext(
                   out var uid,
                   out var health,
                   out var thresholds,
                   out var damageable,
                   out var destructible))
        {
            var desiredBonus = corpseCount * health.HealthPerCorpse;
            var bonusDifference = desiredBonus - health.AppliedCorpseBonus;

            if (bonusDifference == 0)
                continue;

            // Increase the normal mob death threshold.
            if (_mobThreshold.TryGetDeadThreshold(uid, out var currentMaxHealth, thresholds))
            {
                var newMaxHealth = currentMaxHealth.Value + bonusDifference;

                _mobThreshold.SetMobStateThreshold(
                    uid,
                    newMaxHealth,
                    MobState.Dead,
                    thresholds);
            }

            // Increase the destruction threshold by the same amount.
            foreach (var threshold in destructible.Thresholds)
            {
                if (threshold.Trigger is not DamageTrigger damageTrigger)
                    continue;

                damageTrigger.Damage += bonusDifference;
            }

            // Remember how much bonus health has already been applied.
            health.AppliedCorpseBonus = desiredBonus;

            // Re-check the mob state after changing its health threshold.
            _mobThreshold.VerifyThresholds(
                uid,
                thresholds,
                damageable: damageable);
        }
    }
}
