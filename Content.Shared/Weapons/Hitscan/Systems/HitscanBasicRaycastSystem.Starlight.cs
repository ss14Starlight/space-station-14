using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.NullSpace.Components;
using Content.Shared._Starlight.Weapons.Cover.Components;
using Content.Shared._Starlight.Weapons.Hitscan.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Random;
using Content.Shared.Random.Helpers;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicRaycastSystem
{
    private RayCastResults? SelectHit(
        Entity<HitscanBasicRaycastComponent> hitscan,
        EntityUid shooter,
        MapCoordinates from,
        Vector2 direction,
        float pointer,
        EntityUid? target,
        List<RayCastResults> rayCastResults,
        int? seed)
    {
        RayCastResults? result = null;

        if (_container.IsEntityOrParentInContainer(shooter)) // if we are inside a container hit the container
            result = rayCastResults.Count == 0 ? null : rayCastResults[0];
        else
        {
            foreach (var collide in rayCastResults)
            {
                if (collide.Distance == 0) // prevent self-referential loop that results in rounds getting "trapped", awful 3x damage self-crits with guns against 0 distance walls, etc
                    continue;
                // FOR ANYONE TOUCHING HITSCAN ONCE MORE, DO NOT FORGET THE CHECK NullSpaceComponent, This is the Third time i have to FIX IT!
                if (HasComp<NullSpaceComponent>(collide.HitEntity))
                    continue;
                if (collide.HitEntity != target && (CompOrNull<RequireProjectileTargetComponent>(collide.HitEntity)?.Active == true))
                    continue;
                if (!(collide.Distance >= hitscan.Comp.MinDistance || _tag.HasAnyTag(collide.HitEntity, hitscan.Comp.NotArmedCollideWith)))
                    continue;
                // Low cover (flipped tables, sandbags) only catches a share of the shots crossing it.
                if (_cover.PassesOverCover(collide.HitEntity, hitscan.Owner, shooter, collide.Distance, target, direction, seed))
                    continue;
                if (collide.Distance < pointer - 2f && HasComp<MobMoverComponent>(collide.HitEntity))
                {
                    if (pointer - collide.Distance > 4f)
                        continue;

                    var chance = Math.Clamp(1f - ((collide.Distance - 2f) / 2), 0f, 1f);
                    if (!Prob(chance, seed, collide.HitEntity))
                        continue;
                }

                result = collide;
                break;
            }
        }

        if (TryFindCover(hitscan, from, direction, shooter, result?.Distance ?? hitscan.Comp.MaxDistance, target, seed) is { } cover)
            result = cover;

        return result;
    }

    private bool Prob(float chance, int? seed, EntityUid rolledFor)
    {
        if (seed is not { } value)
            return _rand.Prob(chance);

        return new System.Random(HashCode.Combine(value, GetNetEntity(rolledFor).Id)).Prob(chance);
    }

    public HitscanTrace PredictTrace(
        Entity<HitscanBasicRaycastComponent> hitscan,
        EntityUid shooter,
        EntityCoordinates fromCoordinates,
        Vector2 direction,
        float pointer,
        EntityUid? target,
        int seed)
    {
        if (TryComp<MechPilotComponent>(shooter, out var pilot))
            shooter = pilot.Mech;

        var from = _transform.ToMapCoordinates(fromCoordinates);
        var ray = new CollisionRay(from.Position, direction, (int) hitscan.Comp.CollisionMask);
        var rayCastResults = _physics.IntersectRay(from.MapId, ray, hitscan.Comp.MaxDistance, shooter, false).ToList();

        var result = SelectHit(hitscan, shooter, from, direction, pointer, target, rayCastResults, seed);
        var distance = result?.Distance ?? hitscan.Comp.MaxDistance;

        return GenerateTraceStep(fromCoordinates, distance, direction.ToAngle(), result?.HitEntity);
    }

    private RayCastResults? TryFindCover(
        Entity<HitscanBasicRaycastComponent> hitscan,
        MapCoordinates from,
        Vector2 direction,
        EntityUid shooter,
        float maxDistance,
        EntityUid? aimedAt,
        int? seed)
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

            if (_cover.IsShotStopped(hit.HitEntity, hitscan.Owner, shooter, hit.Distance, aimedAt, direction, seed))
                return hit;
        }

        return null;
    }
}
