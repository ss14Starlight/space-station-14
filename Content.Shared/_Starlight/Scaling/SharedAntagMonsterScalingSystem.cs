using Content.Shared.Mobs.Components;
using Content.Shared.Damage.Components;

namespace Content.Shared._Starlight.Scaling;

public abstract partial class SharedScalingSystem : EntitySystem
{
    public void ApplyHealthScaling(
        EntityUid mob,
        EntityUid station,
        AntagMonsterScalingComponent scalingComp,
        MobThresholdsComponent thresholdsComp,
        Dictionary<EntityUid, double> cachedPopulations,
        double universalHealthWeight)
    {
        if (scalingComp.OriginalThresholds == null)
            return;

        var scalingPercent = cachedPopulations[station] * universalHealthWeight;
        scalingPercent = Math.Clamp(
            scalingPercent,
            scalingComp.MinimumHealthScaling,
            scalingComp.MaximumHealthScaling);

        foreach (var threshold in scalingComp.OriginalThresholds)
        {
            var key = threshold.Key;
            var scaledKey = key + key.Double() * scalingPercent;

            if (key == scaledKey)
                continue;

            thresholdsComp.Thresholds.Remove(key);
            thresholdsComp.Thresholds.Add(scaledKey, threshold.Value);
        }

        if (!TryComp<SlowOnDamageComponent>(mob, out var slowOnDamage))
            return;

        var originalSpeedThresholds = slowOnDamage.SpeedModifierThresholds;
        slowOnDamage.SpeedModifierThresholds = new();

        foreach (var speedThreshold in originalSpeedThresholds)
        {
            var scaledSpeedThreshold = speedThreshold.Key
                + speedThreshold.Key.Double() * scalingPercent;

            slowOnDamage.SpeedModifierThresholds.Add(
                scaledSpeedThreshold,
                speedThreshold.Value);
        }
        Dirty(mob, slowOnDamage);
    }
}
