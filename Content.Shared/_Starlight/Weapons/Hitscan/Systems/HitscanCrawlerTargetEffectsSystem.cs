using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Shared._Starlight.Weapons.Hitscan.Systems;

public sealed partial class HitscanCrawlerTargetEffectsSystem : EntitySystem
{
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
        //FarHorizons-edit Start
        var target = args.Data.HitEntity.Value;
        if(TryComp<VehicleComponent>(target, out var vehicle) && HasComp<VehicleBuckleComponent>(target) && vehicle.Rider != null)
            target = vehicle.Rider.Value;

        if (TryComp<CrawlerComponent>(target, out var standing))
        {
            _stunSystem.TryAddStunDuration(target, hitscan.Comp.StunDuration);

            var knockedDown = _stunSystem.TryKnockdown((target, standing), hitscan.Comp.KnockdownDuration, true);

            _movementMod.TryUpdateMovementSpeedModDuration(
                target, //FarHorizons-edit end
                MovementModStatusSystem.TaserSlowdown,
                hitscan.Comp.SlowDuration,
                hitscan.Comp.WalkSpeedMultiplier,
                hitscan.Comp.RunSpeedMultiplier
            );

        if (knockedDown)
            _adminLogger.Add(LogType.DisarmedKnockdown,
                    LogImpact.Medium,
                    $"Projectile {ToPrettyString(hitscan):projectile} shot by {ToPrettyString(args.Data.Shooter):user} from {ToPrettyString(args.Data.Gun):weapon} hit {ToPrettyString(args.Data.HitEntity):target} and knocked them down");
        }
    }
}
