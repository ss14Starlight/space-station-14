using Content.Shared.Mobs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;

namespace Content.Shared._Starlight.Scaling;

public abstract partial class SharedScalingSystem : EntitySystem
{
    public bool ApplyHealthScaling(
        EntityUid mob,
        EntityUid station,
        AntagMonsterScalingComponent scalingComp,
        MobThresholdsComponent thresholdsComp,
        Dictionary<EntityUid, double> cachedPopulations,
        double universalHealthWeight)
    {
        if (scalingComp.OriginalThresholds == null)
            return false;

        var scalingPercent = cachedPopulations[station] * universalHealthWeight;
        scalingPercent = Math.Clamp(
            scalingPercent,
            scalingComp.MinimumHealthScaling,
            scalingComp.MaximumHealthScaling);

        // Build scaled health thresholds without modifying the originals.
        var scaledThresholds = new SortedDictionary<FixedPoint2, MobState>();

        foreach (var threshold in scalingComp.OriginalThresholds)
        {
            var scaledKey = threshold.Key + (threshold.Key.Double() * scalingPercent);

            if (scaledThresholds.ContainsKey(scaledKey))
                return false;

            scaledThresholds.Add(scaledKey, threshold.Value);
        }

        // Build scaled movement thresholds as well.
        var hasSlowOnDamage = TryComp<SlowOnDamageComponent>(mob, out var slowOnDamage);
        Dictionary<FixedPoint2, float>? scaledSpeedThresholds = null;

        if (hasSlowOnDamage)
        {
            scaledSpeedThresholds = new();

            foreach (var speedThreshold in slowOnDamage!.SpeedModifierThresholds)
            {
                var scaledKey = speedThreshold.Key
                    + (speedThreshold.Key.Double() * scalingPercent);

                if (scaledSpeedThresholds.ContainsKey(scaledKey))
                    return false;

                scaledSpeedThresholds.Add(scaledKey, speedThreshold.Value);
            }
        }

        // Commit only after all scaled thresholds have been successfully built.
        thresholdsComp.Thresholds.Clear();

        foreach (var threshold in scaledThresholds)
            thresholdsComp.Thresholds.Add(threshold.Key, threshold.Value);

        if (hasSlowOnDamage)
        {
            slowOnDamage!.SpeedModifierThresholds = scaledSpeedThresholds!;
            Dirty(mob, slowOnDamage);
        }

        return true;
    }
}
