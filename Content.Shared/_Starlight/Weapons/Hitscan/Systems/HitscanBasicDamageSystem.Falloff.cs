using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicDamageSystem
{
    private float GetFalloffMultiplier(EntityUid hitscan, HitscanRaycastFiredData data)
    {
        if (!TryComp<HitscanDamageFalloffComponent>(hitscan, out var falloff))
            return 1f;

        var travelled = 0f;
        foreach (var trace in data.OutputTrace)
        {
            travelled += trace.Distance;
        }

        if (travelled <= falloff.FullDamageDistance)
            return 1f;

        var range = falloff.MinDamageDistance - falloff.FullDamageDistance;
        if (range <= 0f)
            return falloff.MinMultiplier;

        var progress = Math.Clamp((travelled - falloff.FullDamageDistance) / range, 0f, 1f);
        return MathHelper.Lerp(1f, falloff.MinMultiplier, progress);
    }
}
