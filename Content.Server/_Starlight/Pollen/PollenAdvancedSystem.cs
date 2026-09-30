using Content.Server._Starlight.Pollen.Components;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Spawners;
using System.Numerics;

using ServerPollenAdvancedComponent = Content.Server._Starlight.Pollen.Components.PollenAdvancedComponent;

namespace Content.Server._Starlight.Pollen.Systems;

public sealed partial class PollenAdvancedSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId _advancedPollenCloud = "PollenAdvancedPollenCloud";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var perkQuery = EntityQueryEnumerator<PollenAdvancedComponent>();
        while (perkQuery.MoveNext(out var uid, out var perk))
        {
            if (perk.NextSpawn == TimeSpan.Zero)
                perk.NextSpawn = now + RollSpawnDelay(perk);

            if (now < perk.NextSpawn)
                continue;

            var delay = RollSpawnDelay(perk);
            perk.NextSpawn = now + delay;

            var deviation = (float)(delay - TimeSpan.FromSeconds(perk.SpawnInterval)).TotalSeconds;
            var lifetime = Math.Max(0.1f, perk.PollenLifetime + deviation * perk.LifetimeDeviationMultiplier);

            var offset = RandomPollenOffset();
            var cloud = Spawn(_advancedPollenCloud, Transform(uid).Coordinates.Offset(offset));

            if (TryComp<TimedDespawnComponent>(cloud, out var despawn))
                despawn.Lifetime = lifetime;
        }

        var cloudQuery = EntityQueryEnumerator<PollenAdvancedPollenComponent, TransformComponent>();
        while (cloudQuery.MoveNext(out var cloudUid, out var cloud, out var cloudXform))
        {
            if (now < cloud.NextCheck)
                continue;

            cloud.NextCheck = now + TimeSpan.FromSeconds(1);
            CheckAdvancedPollenInteraction(cloudUid, cloud, cloudXform);
        }
    }

    private TimeSpan RollSpawnDelay(PollenAdvancedComponent perk)
    {
        var variance = perk.SpawnInterval * perk.SpawnIntervalDeviation;
        var seconds = perk.SpawnInterval + _random.NextFloat(-variance, variance);
        return TimeSpan.FromSeconds(Math.Max(0.1f, seconds));
    }

    private Vector2 RandomPollenOffset()
    {
        var angle = _random.NextFloat(0f, MathF.Tau);
        var distance = MathF.Sqrt(_random.NextFloat()) * 0.5f;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
    }

    private void CheckAdvancedPollenInteraction(EntityUid cloudUid, PollenAdvancedPollenComponent cloud, TransformComponent cloudXform)
    {
        var nearby = _lookup.GetEntitiesInRange(cloudXform.Coordinates, cloud.CheckRange);

        EntityUid? diona = null;
        EntityUid? allergic = null;
        EntityUid? minded = null;

        foreach (var candidate in nearby)
        {
            if (candidate == cloudUid)
                continue;

            if (diona == null && HasComp<PollenCollectorComponent>(candidate))
            {
                diona = candidate;
                continue;
            }

            if (allergic == null && HasComp<PollenSensitiveComponent>(candidate))
            {
                allergic = candidate;
                continue;
            }

            if (minded == null && _mind.TryGetMind(candidate, out _, out _))
                minded = candidate;
        }

        if (diona is { } dionaUid && _random.Prob(cloud.DionaChance))
        {
            // TODO: Advanced Pollen Diona effect.
            return;
        }

        if (allergic is { } allergicUid && _random.Prob(cloud.AllergicChance))
        {
            _popup.PopupEntity(Loc.GetString("scent-sneeze-allergic"), allergicUid, allergicUid, PopupType.Small);
            return;
        }

        if (minded is { } mindedUid && _random.Prob(cloud.MindChance))
            _popup.PopupEntity(Loc.GetString("pollen-advanced-pollen-mind-message"), mindedUid, mindedUid, PopupType.Small);
    }
}
