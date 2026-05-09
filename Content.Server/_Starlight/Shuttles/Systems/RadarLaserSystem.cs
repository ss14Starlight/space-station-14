using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

/// <summary>
/// Tracks hitscan shuttle gun (e.g. Apollo) laser shots and records their beam data
/// so that <see cref="RadarConsoleSystem"/> can include them as transient laser lines
/// in the radar BUI state.
/// </summary>
public sealed class RadarLaserSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transforms = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarLaserTrackerComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(EntityUid uid, RadarLaserTrackerComponent tracker, ref GunShotEvent args)
    {
        var xform = Transform(uid);
        var mapCoords = _transforms.GetMapCoordinates(uid, xform);

        // The gun fires in the direction of its world rotation (angle zero = east/right; SS14 uses Angle 0 = south,
        // but world rotation on shuttle guns should reflect the direction they're aimed).
        var fireDir = xform.WorldRotation.ToWorldVec();

        // Normalize just in case (should already be a unit vector, but be safe).
        var len = fireDir.Length();
        if (len > 0f)
            fireDir /= len;

        var expiryTime = (float)_timing.CurTime.TotalSeconds + tracker.TraceDuration;
        tracker.Traces.Add((mapCoords, fireDir, expiryTime));
    }

    /// <summary>
    /// Removes expired traces from all <see cref="RadarLaserTrackerComponent"/> instances.
    /// Called by <see cref="RadarConsoleSystem"/> before building the BUI state.
    /// </summary>
    public void PruneExpiredTraces(float currentTime)
    {
        var query = AllEntityQuery<RadarLaserTrackerComponent>();
        while (query.MoveNext(out _, out var tracker))
        {
            tracker.Traces.RemoveAll(t => t.ExpiryTime <= currentTime);
        }
    }
}
