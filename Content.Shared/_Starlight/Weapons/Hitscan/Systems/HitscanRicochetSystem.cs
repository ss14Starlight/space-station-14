using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared._Starlight.Combat.Ranged.Pierce;
using Content.Shared._Starlight.Weapon;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics;
using Robust.Shared.Random;
using System.Linq;
using System.Numerics;

namespace Content.Server._Starlight.Combat.Ranged;

public sealed partial class HitscanRicochetSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _rand = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<HitscanReflectComponent> _reflectQuery;

    public override void Initialize()
    {
        _reflectQuery = GetEntityQuery<HitscanReflectComponent>();

        SubscribeLocalEvent<HitscanRicochetComponent, AttemptHitscanRaycastFiredEvent>(OnHitscanHit);
        SubscribeLocalEvent<RicochetableComponent, HitScanRicochetAttemptEvent>(OnRicochetPierce);
        base.Initialize();
    }

    private void OnHitscanHit(Entity<HitscanRicochetComponent> hitscan, ref AttemptHitscanRaycastFiredEvent args)
    {
        var data = args.Data;

        if (hitscan.Comp.Chance <= 0 || data.HitEntity == null || data.HitPosition == null)
            return;

        // If we're at our maximum recursion depth, don't try to pierce
        if (!_reflectQuery.TryComp(hitscan.Owner, out var reflect) || reflect.CurrentReflections > reflect.MaxReflections)
            return;

        var ev = new HitScanRicochetAttemptEvent(hitscan.Comp.Chance, data.HitPosition.Value, data.ShotDirection, false);
        RaiseLocalEvent(data.HitEntity.Value, ref ev);

        if (!ev.Ricocheted)
            return;

        reflect.CurrentReflections++;

        args.Cancelled = true;

        var fromEffect = Transform(data.HitEntity.Value).Coordinates;

        var hitFiredEvent = new HitscanTraceEvent
        {
            FromCoordinates = fromEffect,
            ShotDirection = ev.Dir,
            Gun = data.Gun,
            Shooter = data.HitEntity.Value,
            OutputTrace = data.OutputTrace,
        };

        RaiseLocalEvent(hitscan, ref hitFiredEvent);
    }

    private void OnRicochetPierce(Entity<RicochetableComponent> ent, ref HitScanRicochetAttemptEvent args)
    {
        if (!TryComp<FixturesComponent>(ent, out var fixtures)
            || fixtures.Fixtures.Count == 0
            || fixtures.Fixtures.FirstOrDefault().Value?.Shape is not PolygonShape shape)
            return;

        var chance = Math.Clamp(args.Chance * ent.Comp.Chance, 0f, 1f);
        if (chance == 0) return;

        var invMatrix = _transform.GetInvWorldMatrix(ent.Owner);

        var localFrom = Vector2.Transform(args.Pos, invMatrix);

        var invNoTrans = invMatrix;
        invNoTrans.M31 = 0f;
        invNoTrans.M32 = 0f;

        var localDir = Vector2.Transform(args.Dir, invNoTrans).Normalized();

        if (!RayCastPolygon(shape, localFrom, localDir,
                 out var tMin, out var edgeIndex, out var ptLocal)) return;

        var localNormal = shape.Normals[edgeIndex];

        var dot = Vector2.Dot(localDir, localNormal);

        var clampedDot = Math.Clamp(MathF.Abs(dot), 0f, 1f);

        var angleFactor = 2f * (1f - clampedDot);

        chance = Math.Clamp(args.Chance * angleFactor, 0f, 1f);
        if(!_rand.Prob(chance)) return;

        //    R = D - 2*(D·N)*N
        var reflectedLocal = localDir - (2f * dot * localNormal);

        var matrix = _transform.GetWorldMatrix(ent.Owner);
        var matrixNoTrans = matrix;
        matrixNoTrans.M31 = 0f;
        matrixNoTrans.M32 = 0f;

        var reflectedWorld = Vector2.Transform(reflectedLocal, matrixNoTrans).Normalized();

        args.Dir = reflectedWorld;
        args.Ricocheted = true;
    }

    private bool RayCastPolygon(
    PolygonShape polygon,
    Vector2 origin,
    Vector2 dir,
    out float tMin,
    out int edgeIndex,
    out Vector2 ptLocal,
    float maxT = float.MaxValue)
    {
        tMin = float.MaxValue;
        edgeIndex = -1;
        ptLocal = default;

        var verts = polygon.Vertices;
        var count = polygon.VertexCount;

        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            var v0 = verts[i];
            var v1 = verts[next];

            if (RayCastSegment(origin, dir, v0, v1, out var t) && t >= 0f && t < maxT)
            {
                if (t < tMin)
                {
                    tMin = t;
                    edgeIndex = i;
                }
            }
        }

        if (edgeIndex < 0)
            return false;

        ptLocal = origin + (dir * tMin);
        return true;
    }
    private bool RayCastSegment(Vector2 origin, Vector2 dir, Vector2 v0, Vector2 v1, out float t)
    {
        t = 0f;

        var edge = v1 - v0;
        var denom = Cross2D(edge, dir);

        if (MathF.Abs(denom) < 1e-6f)
            return false;

        var diff = origin - v0;

        var s = Cross2D(diff, dir) / denom;
        if (s is < 0f or > 1f)
            return false;

        var tRay = Cross2D(diff, edge) / denom;
        if (tRay < 0f)
            return false;

        t = tRay;
        return true;
    }

    private float Cross2D(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
}
