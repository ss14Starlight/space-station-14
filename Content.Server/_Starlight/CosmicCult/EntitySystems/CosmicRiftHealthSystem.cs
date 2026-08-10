using Content.Server._Starlight.CosmicCult.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Starlight.CosmicCult;

public sealed class CosmicRiftHealthSystem : EntitySystem
{
    // Used to get the current number of corpses stored by the empowered rift.
    [Dependency] private readonly CosmicMalignEmpoweredRiftSystem _riftSystem = default!;

    // Used to modify the mob's death threshold, which acts as its maximum health.
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Every stored corpse contributes to the maximum health bonus.
        var corpseCount = _riftSystem.StoredCorpseCount;

        // Find all entities that use the dynamic rift health scaling.
        var query = EntityQueryEnumerator<
            CosmicRiftHealthComponent,
            MobThresholdsComponent>();

        while (query.MoveNext(out var uid, out var health, out var thresholds))
        {
            // Calculate the total health bonus this entity should currently have.
            var desiredBonus = corpseCount * health.HealthPerCorpse;

            // Only apply the difference since the last update.
            // This allows the bonus to increase or decrease when the corpse count changes.
            var bonusDifference = desiredBonus - health.AppliedCorpseBonus;

            if (bonusDifference == 0)
                continue;

            // Get the entity's current maximum health.
            if (!_mobThreshold.TryGetDeadThreshold(uid, out var currentMaxHealth, thresholds))
                continue;

            // Apply the difference to the existing maximum health.
            var newMaxHealth = currentMaxHealth.Value + bonusDifference;

            _mobThreshold.SetMobStateThreshold(
                uid,
                newMaxHealth,
                MobState.Dead,
                thresholds);

            // Remember the bonus we have applied so it is not added again next tick.
            health.AppliedCorpseBonus = desiredBonus;
        }
    }
}
