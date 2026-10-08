using Content.Server._Starlight.Pollen.Components;
using Content.Server._Starlight.Scent.Systems;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Spawners;
using System.Numerics;

namespace Content.Server._Starlight.Pollen;

public sealed partial class PollenAdvancedSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ScentSystem _scent = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId _advancedPollenCloud = "PollenAdvancedPollenCloud";
    private static readonly ProtoId<DamageGroupPrototype> _bruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> _burnGroup = "Burn";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenSpeedBuffComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

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

        var speedBuffQuery = EntityQueryEnumerator<PollenSpeedBuffComponent>();
        while (speedBuffQuery.MoveNext(out var buffUid, out var buff))
        {
            if (now < buff.ExpiresAt)
                continue;

            RemComp<PollenSpeedBuffComponent>(buffUid);
            _movementSpeed.RefreshMovementSpeedModifiers(buffUid);
        }
    }

    private void OnRefreshSpeed(EntityUid uid, PollenSpeedBuffComponent component, RefreshMovementSpeedModifiersEvent args)
        => args.ModifySpeed(component.WalkModifier, component.SprintModifier);

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
            ApplyAdvancedPollenEffect(dionaUid, cloud);
            QueueDel(cloudUid);
            return;
        }

        if (allergic is { } allergicUid && _random.Prob(cloud.AllergicChance))
        {
            ForceSneeze(allergicUid);
            QueueDel(cloudUid);
            return;
        }

        if (minded is { } mindedUid && _random.Prob(cloud.MindChance))
        {
            _popup.PopupEntity(Loc.GetString("pollen-advanced-pollen-mind-message"), mindedUid, mindedUid, PopupType.Small);
            QueueDel(cloudUid);
        }
    }

    private void ApplyAdvancedPollenEffect(EntityUid diona, PollenAdvancedPollenComponent cloud)
    {
        switch (_random.Next(3))
        {
            case 0:
                ApplyHealBuff(diona, cloud);
                _popup.PopupEntity(Loc.GetString("gotheal"), diona, diona, PopupType.Small);
                break;
            case 1:
                ApplySpeedBuff(diona, cloud);
                _popup.PopupEntity(Loc.GetString("gotspeed"), diona, diona, PopupType.Small);
                break;
            default:
                _popup.PopupEntity(Loc.GetString("nothing"),  diona, diona, PopupType.Small);
                break;
        }
    }

    private void ApplyHealBuff(EntityUid diona, PollenAdvancedPollenComponent cloud)
    {
        var heal = new DamageSpecifier(_prototype.Index(_bruteGroup), -cloud.HealBrute);
        heal += new DamageSpecifier(_prototype.Index(_burnGroup), -cloud.HealBurn);
        _damageable.TryChangeDamage(diona, heal, ignoreResistances: true);
    }

    private void ApplySpeedBuff(EntityUid diona, PollenAdvancedPollenComponent cloud)
    {
        var buff = EnsureComp<PollenSpeedBuffComponent>(diona);
        buff.WalkModifier = cloud.SpeedWalkModifier;
        buff.SprintModifier = cloud.SpeedSprintModifier;
        buff.ExpiresAt = _timing.CurTime + cloud.SpeedBuffDuration;
        _movementSpeed.RefreshMovementSpeedModifiers(diona);
    }

    private void ForceSneeze(EntityUid target)
    {
        if (!TryComp(target, out SmellerComponent? smeller))
            return;

        _scent.ForceAllergySneeze((target, smeller), smeller.SmokeLockout);
        _popup.PopupEntity(Loc.GetString("scent-sneeze-allergic"), target, target, PopupType.Small);
    }
}

