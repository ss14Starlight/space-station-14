using Content.Shared.Mobs.Components;

namespace Content.Shared._Starlight.Scaling;

public abstract partial class SharedScalingSystem : EntitySystem
{
    public void ApplyHealthScaling(
        EntityUid station,
        AntagMonsterScalingComponent scalingComp,
        MobThresholdsComponent thresholdsComp,
        Dictionary<EntityUid, double> cachedPopulations,
        double universalHealthWeight)
    {

        if (scalingComp.OriginalThresholds == null)
            return;

        foreach (var threshold in scalingComp.OriginalThresholds)
        {
            var key = threshold.Key;

            var scalingPercent = cachedPopulations[station] * universalHealthWeight;

            if (scalingPercent > scalingComp.MaximumHealthScaling)
                scalingPercent = scalingComp.MaximumHealthScaling;

            if (scalingPercent < scalingComp.MinimumHealthScaling)
                scalingPercent = scalingComp.MinimumHealthScaling;

            var scalingValue = key.Double() * scalingPercent;

            var scaledKey = key + scalingValue;

            if (key != scaledKey)
            {
                thresholdsComp.Thresholds.Remove(key);
                thresholdsComp.Thresholds.Add(scaledKey, threshold.Value);
            }
        }
    }
}
