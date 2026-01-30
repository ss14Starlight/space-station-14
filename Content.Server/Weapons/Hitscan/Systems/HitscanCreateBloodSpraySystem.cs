using System.Linq;
using System.Numerics;
using Content.Server.Decals;
using Content.Server.Weapons.Hitscan.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Decals;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Weapons.Hitscan.Systems;

public sealed class HitscanCreateBloodSpraySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DecalSystem _decal = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    private string[] _bloodDecals = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanCreateBloodSprayComponent, HitscanDamageDealtEvent>(OnHitscanHit);
        CacheDecals();
    }

    // TODO: this should also be updated whenever the protos are updated.
    private void CacheDecals()
    {
        _bloodDecals = _proto.EnumeratePrototypes<DecalPrototype>().Where(x => x.Tags.Contains("BloodSplatter")).Select(x => x.ID).ToArray();
    }

    private void OnHitscanHit(Entity<HitscanCreateBloodSprayComponent> ent, ref HitscanDamageDealtEvent args)
    {
        if (!TryComp<BloodstreamComponent>(args.Data.HitEntity, out var bloodstream))
            return;

        var data = args.Data;

        var gunShooingXform = Transform(args.Data.Gun);
        
        // Copied from HitscanBasicRaycastSystem
        var shotAngle = data.ShotDirection.ToAngle();
        if (TryComp(gunShooingXform.GridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, _) = _transform.GetWorldPositionRotationInvMatrix(gridXform);
            shotAngle -= gridRot;
        }
        else
        {
            return; // TODO: Add logic that actually works for off grid shots.
        }
        
        var distance = Math.Abs((Transform(args.Data.HitEntity.Value).Coordinates.Position - Transform(args.Data.Gun).Coordinates.Position).Length());
        var hitEntityCords = Transform(args.Data.HitEntity.Value).Coordinates;
        var color = _bloodstream.GetBloodColor(args.Data.HitEntity.Value);
        var coords = hitEntityCords.Offset((shotAngle.ToVec() * ((distance/5000.0f) + 1.3f)) + new Vector2(-0.5f, -0.5f));

        Timer.Spawn(200, () =>
        {
            // A flash of the neuralyzer, then a man in a black suit says that you didn’t see any “vector crutch” here, and if you did—read it again.
            _decal.TryAddDecal(_random.Pick(_bloodDecals), coords, out _, color, shotAngle + Angle.FromDegrees(-45), cleanable: true);
        });
    }
}
