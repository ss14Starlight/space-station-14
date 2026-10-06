using System.Numerics;
using Content.Server.Movement.Components;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Weapons.Hitscan.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Weapons.Hitscan.Systems;

public sealed partial class HitscanLagCompensationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float MoveMargin = 3f;

    private bool _enabled;
    private TimeSpan _maxRewind;
    private int _extraTicks;

    private readonly HashSet<Entity<LagCompensationComponent>> _candidates = [];

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.HitscanLagCompensation, value => _enabled = value, true);
        Subs.CVar(_cfg, StarlightCCVars.HitscanLagCompensationMaxMs, value => _maxRewind = TimeSpan.FromMilliseconds(value), true);
        Subs.CVar(_cfg, StarlightCCVars.HitscanLagCompensationExtraTicks, value => _extraTicks = value, true);
    }

    [SubscribeLocalEvent]
    private void OnLagCompensation(ref HitscanLagCompensationEvent ev)
    {
        if (!_enabled || ev.Shooter is not { } shooter || !TryComp<ActorComponent>(shooter, out var actor))
            return;

        var rewind = TimeSpan.FromMilliseconds(actor.PlayerSession.Ping) + (_timing.TickPeriod * _extraTicks);
        if (rewind > _maxRewind)
            rewind = _maxRewind;

        if (rewind <= TimeSpan.Zero)
            return;

        var time = _timing.CurTime - rewind;

        ev.Results.RemoveAll(hit => HasComp<LagCompensationComponent>(hit.HitEntity));

        var end = ev.Origin + (ev.Direction * ev.MaxDistance);
        var bounds = new Box2(Vector2.Min(ev.Origin, end), Vector2.Max(ev.Origin, end)).Enlarged(MoveMargin);

        _candidates.Clear();
        _lookup.GetEntitiesIntersecting(ev.MapId, bounds, _candidates);

        foreach (var target in _candidates)
        {
            if (target.Owner == shooter || target.Owner == ev.Ignored)
                continue;

            if (_container.IsEntityOrParentInContainer(target))
                continue;

            if (!TryComp<FixturesComponent>(target, out var fixtures))
                continue;

            if (!TryGetPastTransform(target, time, out var position, out var rotation) || position.MapId != ev.MapId)
                continue;

            if (!TryIntersect(ev.Origin, ev.Direction, ev.MaxDistance, ev.CollisionMask, fixtures, new Transform(position.Position, rotation), out var distance))
                continue;

            ev.Results.Add(new RayCastResults(distance, ev.Origin + (ev.Direction * distance), target));
        }

        ev.Results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
    }

    private bool TryGetPastTransform(Entity<LagCompensationComponent> ent, TimeSpan time, out MapCoordinates position, out Angle rotation)
    {
        var xform = Transform(ent);
        var coordinates = xform.Coordinates;
        var localRotation = xform.LocalRotation;
        var found = false;

        foreach (var (moved, pastCoordinates, pastRotation) in ent.Comp.Positions)
        {
            if (moved > time && found)
                break;

            coordinates = pastCoordinates;
            localRotation = pastRotation;
            found = true;

            if (moved > time)
                break;
        }

        position = MapCoordinates.Nullspace;
        rotation = Angle.Zero;

        if (!coordinates.IsValid(EntityManager))
            return false;

        position = _transform.ToMapCoordinates(coordinates);
        rotation = _transform.GetWorldRotation(coordinates.EntityId) + localRotation;
        return true;
    }

    private static bool TryIntersect(
        Vector2 origin,
        Vector2 direction,
        float maxDistance,
        int collisionMask,
        FixturesComponent fixtures,
        Transform transform,
        out float distance)
    {
        distance = float.MaxValue;

        // Distances are preserved by the rigid transform, so the ray is tested in each shape's local frame.
        var localOrigin = Robust.Shared.Physics.Transform.InvTransformPoint(transform, origin);
        var localDirection = Quaternion2D.InvRotateVector(transform.Quaternion2D, direction);

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard || (fixture.CollisionLayer & collisionMask) == 0)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                if (IntersectRayShape(localOrigin, localDirection, fixture.Shape, i, out var hit) && hit < distance)
                    distance = hit;
            }
        }

        return distance <= maxDistance;
    }

    private static bool IntersectRayShape(Vector2 origin, Vector2 direction, IPhysShape shape, int childIndex, out float distance)
    {
        distance = 0f;

        switch (shape)
        {
            case PhysShapeCircle circle:
                return IntersectRayCircle(origin, direction, circle.Position, circle.Radius, out distance);
            case PolygonShape polygon:
                return IntersectRayPolygon(origin, direction, polygon.Vertices, polygon.Normals, out distance);
            case PhysShapeAabb aabb:
                return IntersectRayBox(origin, direction, aabb.LocalBounds, out distance);
            case ChainShape chain:
            {
                var i2 = childIndex + 1;
                if (i2 == chain.Count)
                    i2 = 0;

                return IntersectRaySegment(origin, direction, chain.Vertices[childIndex], chain.Vertices[i2], out distance);
            }
            default:
                return false;
        }
    }

    private static bool IntersectRayCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius, out float distance)
    {
        distance = 0f;

        var s = origin - center;
        var a = direction.LengthSquared();
        if (a < 1e-12f)
            return false;

        var b = Vector2.Dot(s, direction);
        var c = s.LengthSquared() - (radius * radius);

        // Origin inside the circle.
        if (c <= 0f)
            return true;

        var discriminant = (b * b) - (a * c);
        if (b > 0f || discriminant < 0f)
            return false;

        distance = (-b - MathF.Sqrt(discriminant)) / a;
        return true;
    }

    // Convex polygon with outward normals (Cyrus-Beck clipping).
    private static bool IntersectRayPolygon(Vector2 origin, Vector2 direction, Vector2[] vertices, Vector2[] normals, out float distance)
    {
        distance = 0f;

        if (vertices.Length < 3 || normals.Length < vertices.Length)
            return false;

        var lower = 0f;
        var upper = float.MaxValue;

        for (var i = 0; i < vertices.Length; i++)
        {
            var numerator = Vector2.Dot(normals[i], vertices[i] - origin);
            var denominator = Vector2.Dot(normals[i], direction);

            if (denominator == 0f)
            {
                if (numerator < 0f)
                    return false;

                continue;
            }

            var t = numerator / denominator;
            if (denominator < 0f)
                lower = MathF.Max(lower, t);
            else
                upper = MathF.Min(upper, t);

            if (upper < lower)
                return false;
        }

        distance = lower;
        return true;
    }

    private static bool IntersectRaySegment(Vector2 origin, Vector2 direction, Vector2 v1, Vector2 v2, out float distance)
    {
        distance = 0f;

        var edge = v2 - v1;
        var denominator = Cross(direction, edge);
        if (MathF.Abs(denominator) < 1e-9f)
            return false;

        var offset = v1 - origin;
        var t = Cross(offset, edge) / denominator;
        var u = Cross(offset, direction) / denominator;

        if (t < 0f || u < 0f || u > 1f)
            return false;

        distance = t;
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
        => (a.X * b.Y) - (a.Y * b.X);

    private static bool IntersectRayBox(Vector2 origin, Vector2 direction, Box2 box, out float distance)
    {
        var tMin = 0f;
        var tMax = float.MaxValue;
        distance = 0f;

        for (var axis = 0; axis < 2; axis++)
        {
            var o = axis == 0 ? origin.X : origin.Y;
            var d = axis == 0 ? direction.X : direction.Y;
            var min = axis == 0 ? box.Left : box.Bottom;
            var max = axis == 0 ? box.Right : box.Top;

            if (MathF.Abs(d) < 1e-6f)
            {
                if (o < min || o > max)
                    return false;

                continue;
            }

            var t1 = (min - o) / d;
            var t2 = (max - o) / d;
            if (t1 > t2)
                (t1, t2) = (t2, t1);

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);

            if (tMin > tMax)
                return false;
        }

        distance = tMin;
        return true;
    }
}
