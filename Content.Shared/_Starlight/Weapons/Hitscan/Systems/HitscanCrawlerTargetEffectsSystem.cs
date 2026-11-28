using Content.Shared.Vehicle.Systems;
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Shared._Starlight.Weapons.Hitscan.Systems;

public sealed partial class HitscanCrawlerTargetEffectsSystem : EntitySystem
{
    [Dependency] private VehicleSystem _rideVehicles = default!; // Starlight
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanCrawlerTargetEffectsComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanCrawlerTargetEffectsComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        var target = _rideVehicles.GetExposedOperatorOrSelf(args.Data.HitEntity.Value); // Starlight
        if (TryComp<CrawlerComponent>(target, out var standing))
        {
            _stunSystem.TryAddStunDuration(target, hitscan.Comp.StunDuration);

            var knockedDown = _stunSystem.TryKnockdown((target, standing), hitscan.Comp.KnockdownDuration, true);

            _movementMod.TryUpdateMovementSpeedModDuration(
                target,
                MovementModStatusSystem.TaserSlowdown,
                hitscan.Comp.SlowDuration,
                hitscan.Comp.WalkSpeedMultiplier,
                hitscan.Comp.RunSpeedMultiplier
            );

        if (knockedDown)
            _adminLogger.Add(LogType.DisarmedKnockdown,
                    LogImpact.Medium,
                    $"Projectile {ToPrettyString(hitscan):projectile} shot by {ToPrettyString(args.Data.Shooter):user} from {ToPrettyString(args.Data.Gun):weapon} hit {ToPrettyString(target):target} and knocked them down");
        }
    }
}
