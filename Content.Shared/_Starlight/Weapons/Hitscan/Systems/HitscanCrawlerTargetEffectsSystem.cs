using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanCrawlerTargetEffectsSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;

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

            _stunSystem.TryKnockdown((args.Data.HitEntity.Value, standing), hitscan.Comp.KnockdownDuration, true);

            _movementMod.TryUpdateMovementSpeedModDuration(
                args.Data.HitEntity.Value,
                MovementModStatusSystem.TaserSlowdown,
                hitscan.Comp.SlowDuration,
                hitscan.Comp.WalkSpeedMultiplier,
                hitscan.Comp.RunSpeedMultiplier
            );
        }
    }
}
