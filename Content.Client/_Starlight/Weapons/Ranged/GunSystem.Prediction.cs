using System.Numerics;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Weapons.Hitscan.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private HitscanBasicRaycastSystem _hitscan = default!;

    private static readonly TimeSpan _pendingHitscanTimeout = TimeSpan.FromSeconds(2);

    private const float MechMuzzleOffset = 0.8f;

    private bool _hitscanPrediction = true;

    private const double MispredictAngleDegrees = 1;
    private const float MispredictDistance = 0.5f;

    private readonly List<PendingHitscan> _pendingHitscans = [];

    /// <summary>
    /// Effects of a predicted trace, kept so they can be removed if the server disagrees with the prediction.
    /// </summary>
    private sealed class PredictedHitscanEffects
    {
        public readonly List<EntityUid> Entities = [];
        public bool Cancelled;
    }

    private readonly record struct PendingHitscan(NetEntity Gun, int Seed, TimeSpan Expires, List<HitscanTrace> Predicted, PredictedHitscanEffects Effects, string? Prototype);

    /// <summary>
    /// Set while a predicted trace is being rendered, so every spawned effect gets recorded.
    /// </summary>
    private PredictedHitscanEffects? _recordingEffects;

    private void InitializePrediction()
        => Subs.CVar(_cfg, StarlightCCVars.HitscanPrediction, value => _hitscanPrediction = value, true);

    private bool CanPredictHitscan(EntityUid? user)
        => _hitscanPrediction && Timing.IsFirstTimePredicted && user != null && user == _player.LocalEntity;

    private void PredictCartridge(Entity<GunComponent> gun, EntProtoId round, int ammoIndex, MapCoordinates from, Vector2 mapDirection, EntityUid? user)
    {
        if (!CanPredictHitscan(user) || !ProtoManager.TryIndex(round, out var proto))
            return;

        if (!proto.TryComp<HitscanBasicRaycastComponent>(out _, Factory))
            return;

        if (!proto.TryComp<ProjectileSpreadComponent>(out var spread, Factory))
        {
            PredictHitscan(gun, round, GetHitscanSeed(gun, ammoIndex, 0), MuzzleFrom(from, mapDirection, user), mapDirection.Normalized(), mapDirection.Length(), user);
            return;
        }

        var angles = GetPelletAngles(gun, spread, mapDirection.ToAngle(), ammoIndex);
        for (var i = 0; i < angles.Length; i++)
        {
            var pellet = i == 0 ? round : spread.Proto;
            var direction = angles[i].ToVec();
            PredictHitscan(gun, pellet, GetHitscanSeed(gun, ammoIndex, i), MuzzleFrom(from, direction, user), direction, 1f, user);
        }
    }

    private void PredictHitscan(Entity<GunComponent> gun, EntProtoId round, int seed, MapCoordinates from, Vector2 direction, float pointer, EntityUid? user)
    {
        var shot = Spawn(round, MapCoordinates.Nullspace);
        PredictHitscan(gun, shot, seed, from, direction, pointer, user);
        Del(shot);
    }

    private void PredictHitscan(Entity<GunComponent> gun, EntityUid shot, int seed, MapCoordinates from, Vector2 direction, float pointer, EntityUid? user)
    {
        if (!CanPredictHitscan(user)
            || !TryComp<HitscanBasicRaycastComponent>(shot, out var raycast)
            || !TryComp<HitscanBasicVisualsComponent>(shot, out var visuals)
            || from.MapId == MapId.Nullspace)
            return;

        var fromCoordinates = MapManager.TryFindGridAt(from, out var gridUid, out _)
            ? TransformSystem.ToCoordinates(gridUid, from)
            : TransformSystem.ToCoordinates(from);

        var traces = _hitscan.PredictTrace((shot, raycast), user!.Value, fromCoordinates, direction, pointer, gun.Comp.Target, seed);

        var ev = new HitscanEvent
        {
            MuzzleFlash = visuals.MuzzleFlash,
            TravelFlash = visuals.TravelFlash,
            ImpactFlash = visuals.ImpactFlash,
            Bullet = visuals.Bullet,
            Speed = visuals.Speed,
            Traces = traces,
        };

        var effects = new PredictedHitscanEffects();
        var delay = 0f;
        foreach (var trace in traces)
        {
            delay = FireEffect(ev, delay, trace, effects);
        }

        if (TryComp<HitscanBasicEffectsComponent>(shot, out var impactEffects))
            PlayPredictedImpactSound(CompOrNull<HitscanBasicDamageComponent>(shot), impactEffects, traces[0]);

        _pendingHitscans.Add(new PendingHitscan(GetNetEntity(gun), seed, Timing.RealTime + _pendingHitscanTimeout, traces, effects,
            MetaData(shot).EntityPrototype?.ID));
    }

    private MapCoordinates MuzzleFrom(MapCoordinates from, Vector2 direction, EntityUid? user)
    {
        if (!HasComp<MechPilotComponent>(user))
            return from;

        return from.Offset(direction.Normalized() * MechMuzzleOffset);
    }

    private int TryConsumePredictedHitscan(HitscanEvent ev)
    {
        if (_pendingHitscans.Count == 0
            || ev.Shooter is not { } shooter
            || ev.Gun is not { } gun
            || ev.PredictionSeed is not { } seed
            || GetEntity(shooter) != _player.LocalEntity)
            return 0;

        var now = Timing.RealTime;
        _pendingHitscans.RemoveAll(pending => pending.Expires < now);

        var index = _pendingHitscans.FindIndex(pending => pending.Gun == gun && pending.Seed == seed);
        var seedMatched = index >= 0;

        if (!seedMatched)
            index = _pendingHitscans.FindIndex(pending => pending.Gun == gun);

        if (index < 0)
            return 0;

        var pending = _pendingHitscans[index];
        _pendingHitscans.RemoveAt(index);

        if (seedMatched
            && ev.Traces.Count >= pending.Predicted.Count
            && (ev.Prototype == null || ev.Prototype == pending.Prototype)
            && IsPredictedCorrectly(gun, pending.Predicted, ev.Traces))
            return pending.Predicted.Count;

        // The server disagrees: drop what we drew and let the authoritative trace render instead.
        CancelPredictedEffects(pending.Effects);
        return 0;
    }

    private bool IsPredictedCorrectly(NetEntity gun, List<HitscanTrace> predicted, List<HitscanTrace> actual)
    {
        for (var i = 0; i < predicted.Count; i++)
        {
            if (!IsPredictedCorrectly(gun, predicted[i], actual[i]))
                return false;
        }

        return true;
    }

    private void CancelPredictedEffects(PredictedHitscanEffects effects)
    {
        effects.Cancelled = true;

        foreach (var ent in effects.Entities)
        {
            if (!TerminatingOrDeleted(ent))
                QueueDel(ent);
        }

        effects.Entities.Clear();
    }

    private void RecordPredictedEffect(EntityUid ent)
        => _recordingEffects?.Entities.Add(ent);

    private bool IsOwnPredictedShot(HitscanEvent ev)
        => _hitscanPrediction
        && ev.Shooter is { } shooter
        && GetEntity(shooter) == _player.LocalEntity;

    private void PlayPredictedImpactSound(HitscanEvent ev)
    {
        if (ev.Traces.Count == 0
            || ev.Prototype == null
            || !ProtoManager.TryIndex<EntityPrototype>(ev.Prototype, out var proto)
            || !proto.TryComp<HitscanBasicEffectsComponent>(out var effects, Factory))
            return;

        _ = proto.TryComp<HitscanBasicDamageComponent>(out var damage, Factory);
        PlayPredictedImpactSound(damage, effects, ev.Traces[0]);
    }

    private void PlayPredictedImpactSound(HitscanBasicDamageComponent? damage, HitscanBasicEffectsComponent effects, HitscanTrace trace)
    {
        if (trace.ImpactedEnt is not { } netHit
            || !TryGetEntity(netHit, out var hit)
            || !HasComp<DamageableComponent>(hit))
            return;

        var sound = GetImpactSound(hit.Value, damage?.Damage, effects.Sound, effects.ForceSound, out var variation);
        Audio.PlayPredicted(sound, hit.Value, _player.LocalEntity,
            variation ? AudioParams.Default.WithVariation(DamagePitchVariation) : null);
    }

    private bool IsPredictedCorrectly(NetEntity gun, HitscanTrace predicted, HitscanTrace actual)
    {
        var angleDiff = Math.Abs(Angle.ShortestDistance(predicted.Angle, actual.Angle).Degrees);
        var distanceDiff = Math.Abs(predicted.Distance - actual.Distance);

        if (angleDiff <= MispredictAngleDegrees
            && distanceDiff <= MispredictDistance
            && predicted.ImpactedEnt == actual.ImpactedEnt)
            return true;

        var cause = angleDiff > MispredictAngleDegrees ? "direction" : "world";
        Log.Debug($"Mispredicted hitscan from {ToPrettyString(GetEntity(gun))}, {cause}: "
            + $"angle {predicted.Angle.Degrees:F3} vs {actual.Angle.Degrees:F3}, "
            + $"distance {predicted.Distance:F2} vs {actual.Distance:F2}, "
            + $"hit {ToPrettyString(GetEntity(predicted.ImpactedEnt))} vs {ToPrettyString(GetEntity(actual.ImpactedEnt))}");
        return false;
    }
}
