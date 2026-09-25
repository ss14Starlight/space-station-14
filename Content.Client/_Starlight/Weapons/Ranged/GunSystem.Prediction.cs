using System.Numerics;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Weapons.Hitscan.Events;
using Content.Shared.Mech.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Systems;
using Content.Shared.Weapons.Ranged.Components;
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

    private const double MispredictAngleDegrees = 0.01;
    private const float MispredictDistance = 0.05f;

    private readonly List<(NetEntity Gun, TimeSpan Expires, HitscanTrace Predicted)> _pendingHitscans = new();

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

        var fromCoordinates = TransformSystem.ToCoordinates(from);
        var trace = _hitscan.PredictTrace((shot, raycast), user!.Value, fromCoordinates, direction, pointer, gun.Comp.Target, seed);
        var traces = new List<HitscanTrace>
        {
            trace
        };

        var ev = new HitscanEvent
        {
            MuzzleFlash = visuals.MuzzleFlash,
            TravelFlash = visuals.TravelFlash,
            ImpactFlash = visuals.ImpactFlash,
            Bullet = visuals.Bullet,
            Speed = visuals.Speed,
            Traces = traces,
        };

        FireEffect(ev, 0f, trace);
        _pendingHitscans.Add((GetNetEntity(gun), Timing.RealTime + _pendingHitscanTimeout, trace));
    }

    private MapCoordinates MuzzleFrom(MapCoordinates from, Vector2 direction, EntityUid? user)
    {
        if (!HasComp<MechPilotComponent>(user))
            return from;

        return from.Offset(direction.Normalized() * MechMuzzleOffset);
    }

    private bool TryConsumePredictedHitscan(HitscanEvent ev)
    {
        if (_pendingHitscans.Count == 0
            || ev.Shooter is not { } shooter
            || ev.Gun is not { } gun
            || GetEntity(shooter) != _player.LocalEntity)
            return false;

        var now = Timing.RealTime;
        _pendingHitscans.RemoveAll(pending => pending.Expires < now);

        var index = _pendingHitscans.FindIndex(pending => pending.Gun == gun);
        if (index < 0)
            return false;

        var predicted = _pendingHitscans[index].Predicted;
        _pendingHitscans.RemoveAt(index);

        if (ev.Traces.Count > 0)
            CheckMisprediction(gun, predicted, ev.Traces[0]);

        return true;
    }

    private void CheckMisprediction(NetEntity gun, HitscanTrace predicted, HitscanTrace actual)
    {
        var angleDiff = Math.Abs(Angle.ShortestDistance(predicted.Angle, actual.Angle).Degrees);
        var distanceDiff = Math.Abs(predicted.Distance - actual.Distance);

        if (angleDiff <= MispredictAngleDegrees
            && distanceDiff <= MispredictDistance
            && predicted.ImpactedEnt == actual.ImpactedEnt)
            return;

        var cause = angleDiff > MispredictAngleDegrees ? "direction" : "world";
        Log.Debug($"Mispredicted hitscan from {ToPrettyString(GetEntity(gun))}, {cause}: "
            + $"angle {predicted.Angle.Degrees:F3} vs {actual.Angle.Degrees:F3}, "
            + $"distance {predicted.Distance:F2} vs {actual.Distance:F2}, "
            + $"hit {ToPrettyString(GetEntity(predicted.ImpactedEnt))} vs {ToPrettyString(GetEntity(actual.ImpactedEnt))}");
    }
}
