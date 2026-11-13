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

        if (TryComp<CrawlerComponent>(args.Data.HitEntity.Value, out var standing))
        {
            _stunSystem.TryAddStunDuration(args.Data.HitEntity.Value, hitscan.Comp.StunDuration);

            var knockedDown = _stunSystem.TryKnockdown((args.Data.HitEntity.Value, standing), hitscan.Comp.KnockdownDuration, true);

            _movementMod.TryUpdateMovementSpeedModDuration(
                args.Data.HitEntity.Value,
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
