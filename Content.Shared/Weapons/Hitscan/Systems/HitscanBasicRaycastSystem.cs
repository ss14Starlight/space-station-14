using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

#region Starlight
using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.NullSpace;
using System.Reflection;
using Content.Shared.Movement.Components;
using Robust.Shared.Random;
#endregion Starlight

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanBasicRaycastSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ISharedAdminLogManager _log = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _rand = default!; // Starlight-edit

    private EntityQuery<HitscanBasicVisualsComponent> _visualsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _visualsQuery = GetEntityQuery<HitscanBasicVisualsComponent>();

        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanTraceEvent>(OnHitscanFired);
    }

    private void OnHitscanFired(Entity<HitscanBasicRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        // Starlight start - handle the shooter being the mech, not the pilot
        if (TryComp<MechPilotComponent>(shooter, out var pilotA))
            shooter = pilotA.Mech;
        // Starlight end

        var mapCords = _transform.ToMapCoordinates(args.FromCoordinates);

        // Starlight-start
        var toMap = _transform.ToMapCoordinates(args.ToCoordinates);
        var pointer = (toMap.Position - mapCords.Position).Length();
        RayCastResults? result = null;
        for (var reflectAttempt = 0; reflectAttempt < ent.Comp.Steps; reflectAttempt++ )
        {
            var ray = new CollisionRay(mapCords.Position, args.ShotDirection, (int)ent.Comp.CollisionMask);
            var rayCastResults = _physics.IntersectRay(mapCords.MapId, ray, ent.Comp.MaxDistance, shooter, false).ToList();

            if (rayCastResults.Count == 0)
                break;

            result = rayCastResults[0];

            if (!_container.IsEntityOrParentInContainer(shooter))
            {
                foreach (var collide in rayCastResults)
                {
                    if (collide.HitEntity != args.Target && CompOrNull<RequireProjectileTargetComponent>(collide.HitEntity)?.Active == true)
                        continue;
                    if (collide.Distance < pointer - 2f && HasComp<MobMoverComponent>(collide.HitEntity))
                    {
                        if (pointer - collide.Distance > 4f) continue;

                        var chance = Math.Clamp(1f - ((collide.Distance - 2f) / 2), 0f, 1f);
                        if (!_rand.Prob(chance)) continue;
                    }

                    result = collide;
                    break;
                }
            }
        }

        var distanceTried = result?.Distance ?? ent.Comp.MaxDistance;

        var isRoot = false;
        if (args.OutputTrace is null)
        {
            args.OutputTrace = new List<HitscanTrace>();
            isRoot = true;
        }
        // Starlight end

        // Do visuals without an event. They should always happen and putting it on the attempt event is weird!
        // If more stuff gets added here, it should probably be turned into an event.
        // FireEffects(args.FromCoordinates, distanceTried, args.ShotDirection.ToAngle(), ent.Owner); // Starlight - comment out, as we want to aggregate these
        
        args.OutputTrace.Add(GenerateTraceStep(args.FromCoordinates, distanceTried, args.ShotDirection.ToAngle(), result?.HitEntity)); // Starlight - add the visuals for this particular leg of the hitscan into the trace

        // Admin logging
        if (result?.HitEntity != null)
        {
            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(result.Value.HitEntity):target}"
                + $" using {ToPrettyString(args.Gun):entity}.");
        }

        var data = new HitscanRaycastFiredData
        {
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            HitEntity = result?.HitEntity,
            OutputTrace = args.OutputTrace, // Starlight
            HitPosition = result?.HitPos, // Starlight
        };

        var attemptEvent = new AttemptHitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref attemptEvent);

        if (attemptEvent.Cancelled)
        { // Starlight start - added block with additional command before return
            if (isRoot)
                FireEffects(ent, args.OutputTrace);
            // Starlight end
            return;
        } // Starlight

        var hitEvent = new HitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref hitEvent);

        // Starlight start
        if (isRoot)
            FireEffects(ent, args.OutputTrace);
        // Starlight end
    }

    private HitscanTrace GenerateTraceStep(EntityCoordinates fromCoordinates, float distance, Angle shotAngle, EntityUid? entity = null)// Starlight-edit
    {
        var fromXform = Transform(fromCoordinates.EntityId);

        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoordinates.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = _transform.GetWorldPositionRotationInvMatrix(gridXform);
            var map = _transform.ToMapCoordinates(fromCoordinates);
            fromCoordinates = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            shotAngle -= gridRot;
        }
        else
        {
            shotAngle -= _transform.GetWorldRotation(fromXform);
        }

        var shotVec = shotAngle.ToVec().Normalized();

        return new() {
            Angle = shotAngle,
            Distance = distance,
            // We don't draw muzzle or travel effects if we're at point-blank range, just impact effects
            MuzzleCoordinates = distance > 1f ? GetNetCoordinates(fromCoordinates.Offset(shotVec / 2)) : null,
            TravelCoordinates = distance > 1f ? GetNetCoordinates(fromCoordinates.Offset(shotVec * (distance + 0.5f) / 2)) : null,
            ImpactCoordinates = GetNetCoordinates(fromCoordinates.Offset(shotVec * distance)),
            ImpactedEnt = GetNetEntity(entity),// Starlight-edit
        };
    }

    private void FireEffects(EntityUid hitscan, List<HitscanTrace> traces)
    {
        if (!_visualsQuery.TryComp(hitscan, out var visuals))
        {
            // We're not going to display anything, so don't fire the event to display things
            return;
        }

        // Trigger the render
        var hitscanEvent = new SharedGunSystem.HitscanEvent
        {
            MuzzleFlash = visuals.MuzzleFlash,
            TravelFlash = visuals.TravelFlash,
            ImpactFlash = visuals.ImpactFlash,
            Bullet = visuals.Bullet,
            Speed = visuals.Speed,
            Traces = traces,
        };

        // Figure out who might see the event on any of the bounces
        var filter = Filter.Empty();
        var sampledPositions = traces
                .Select(x => GetCoordinates(x.MuzzleCoordinates))
                .Where(x => x is not null)
                .Select<EntityCoordinates?, EntityCoordinates>(x => x!.Value)
                .Where(x => x.IsValid(EntityManager))
                .ToList();
        foreach (var pos in sampledPositions)
            filter.Merge(Filter.Pvs(pos, entityMan: EntityManager));

        RaiseNetworkEvent(hitscanEvent, filter);
    }

    /* Starlight - comment out upstream FireEffects
    /// <summary>
    /// Create visual effects for the fired hitscan weapon.
    /// </summary>
    /// <param name="fromCoordinates">Location to start the effect.</param>
    /// <param name="distance">Distance of the hitscan shot.</param>
    /// <param name="shotAngle">Angle of the shot.</param>
    /// <param name="hitscanUid">The hitscan entity itself.</param>
    private void FireEffects(EntityCoordinates fromCoordinates, float distance, Angle shotAngle, EntityUid hitscanUid)
    {
        if (distance == 0 || !_visualsQuery.TryComp(hitscanUid, out var vizComp))
            return;

        var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale)>();
        var fromXform = Transform(fromCoordinates.EntityId);

        // We'll get the effects relative to the grid / map of the firer
        // Look you could probably optimise this a bit with redundant transforms at this point.

        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoordinates.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = _transform.GetWorldPositionRotationInvMatrix(gridXform);
            var map = _transform.ToMapCoordinates(fromCoordinates);
            fromCoordinates = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            shotAngle -= gridRot;
        }
        else
        {
            shotAngle -= _transform.GetWorldRotation(fromXform);
        }

        if (distance >= 1f)
        {
            if (vizComp.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(shotAngle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, shotAngle, vizComp.MuzzleFlash, 1f));
            }

            if (vizComp.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(shotAngle.ToVec() * (distance + 0.5f) / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, shotAngle, vizComp.TravelFlash, distance - 1.5f));
            }
        }

        if (vizComp.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(shotAngle.ToVec() * distance);
            var netCoords = GetNetCoordinates(coords);

            sprites.Add((netCoords, shotAngle.FlipPositive(), vizComp.ImpactFlash, 1f));
        }

        if (sprites.Count > 0)
        {
            RaiseNetworkEvent(new SharedGunSystem.HitscanEvent
            {
                Sprites = sprites,
            }, Filter.Pvs(fromCoordinates, entityMan: EntityManager));
        }
    }
    */// Starlight - end of commenting out upstream FireEffects
}
