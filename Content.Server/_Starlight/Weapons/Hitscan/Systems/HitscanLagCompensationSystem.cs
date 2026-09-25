using System.Numerics;
using Content.Server.Movement.Components;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Weapons.Hitscan.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
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

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard || (fixture.CollisionLayer & collisionMask) == 0)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var aabb = fixture.Shape.ComputeAABB(transform, i);
                if (IntersectRayBox(origin, direction, aabb, out var hit) && hit < distance)
                    distance = hit;
            }
        }

        return distance <= maxDistance;
    }

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
