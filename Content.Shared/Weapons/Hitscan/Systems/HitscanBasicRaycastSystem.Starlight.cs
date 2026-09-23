using System.Numerics;
using Content.Shared._Starlight.Weapons.Cover.Components;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicRaycastSystem
{
    private RayCastResults? TryFindCover(
        Entity<HitscanBasicRaycastComponent> hitscan,
        MapCoordinates from,
        Vector2 direction,
        EntityUid shooter,
        float maxDistance)
    {
        if (maxDistance <= 0f)
            return null;

        var ray = new CollisionRay(from.Position, direction, (int)CollisionGroup.BulletImpassable);

        foreach (var hit in _physics.IntersectRay(from.MapId, ray, maxDistance, shooter, false))
        {
            if (hit.Distance <= 0)
                continue;

            if (!HasComp<ProjectileCoverComponent>(hit.HitEntity))
                continue;

            if (_cover.IsShotStopped(hit.HitEntity, hitscan.Owner, shooter, hit.Distance))
                return hit;
        }

        return null;
    }
}
