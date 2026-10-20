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
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared._Starlight.Weapons.Hitscan.Systems;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicRaycastSystem
{
    [Dependency] private PierceSystem _pierce = default!;

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
        // Low cover that bullets normally fly over (counters, crates) still catches a shot at someone lying behind it.
        else if (result is { } hit && target is { } aimed && hit.HitEntity == aimed
            && TryShelterHit(hitscan, from, direction, shooter, aimed, hit.Distance, seed) is { } shelterHit)
            result = shelterHit;

        return result;
    }

    private RayCastResults? TryShelterHit(
        Entity<HitscanBasicRaycastComponent> hitscan,
        MapCoordinates from,
        Vector2 direction,
        EntityUid? shooter,
        EntityUid target,
        float targetDistance,
        int? seed)
    {
        if (direction.LengthSquared() <= 0f)
            return null;

        var normal = Vector2.Normalize(direction);

        _shelters.Clear();
        foreach (var shelter in _cover.GetShelters(target, normal))
        {
            var along = Vector2.Dot(_transform.GetWorldPosition(shelter) - from.Position, normal);
            if (along > 0f && along < targetDistance)
                _shelters.Add((shelter, along, GetNetEntity(shelter).Id));
        }

        _shelters.Sort((a, b) => a.Along != b.Along ? a.Along.CompareTo(b.Along) : a.NetId.CompareTo(b.NetId));

        foreach (var (shelter, along, _) in _shelters)
        {
            if (_cover.IsShotStopped(shelter, hitscan.Owner, shooter, along, target, normal, seed))
                return new RayCastResults(along, from.Position + (normal * along), shelter);
        }

        return null;
    }

    private readonly List<(EntityUid Shelter, float Along, int NetId)> _shelters = new();

    private bool Prob(float chance, int? seed, EntityUid rolledFor)
    {
        if (seed is not { } value)
            return _rand.Prob(chance);

#pragma warning disable CS0618
        return new System.Random(SharedRandomExtensions.HashCodeCombine(value, GetNetEntity(rolledFor).Id)).Prob(chance);
#pragma warning restore CS0618
    }

    public List<HitscanTrace> PredictTrace(
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

        var traces = new List<HitscanTrace>();
        int? legSeed = seed;

        while (true)
        {
            var from = _transform.ToMapCoordinates(fromCoordinates);
            var ray = new CollisionRay(from.Position, direction, (int) hitscan.Comp.CollisionMask);
            var rayCastResults = _physics.IntersectRay(from.MapId, ray, hitscan.Comp.MaxDistance, shooter, false).ToList();

            var result = SelectHit(hitscan, shooter, from, direction, pointer, target, rayCastResults, legSeed);
            var distance = result?.Distance ?? hitscan.Comp.MaxDistance;

            traces.Add(GenerateTraceStep(fromCoordinates, distance, direction.ToAngle(), result?.HitEntity));

            if (result is not { } hit
                || !TryComp<HitscanPierceComponent>(hitscan, out var pierce)
                || !_pierce.TryPierce((hitscan, pierce), hit.HitEntity, direction, legSeed, out var next)
                || !TryComp<HitscanReflectComponent>(hitscan, out var reflect)
                || Transform(hit.HitEntity).MapUid is not { } hitMap)
                break;

            reflect.CurrentReflections++;
            legSeed = PierceSystem.GetNextSeed(legSeed, reflect.CurrentReflections);
            fromCoordinates = new EntityCoordinates(hitMap, hit.HitPos);
            shooter = hit.HitEntity;
            target = null;
            pointer = 1f;
            direction = next;
        }

        return traces;
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
