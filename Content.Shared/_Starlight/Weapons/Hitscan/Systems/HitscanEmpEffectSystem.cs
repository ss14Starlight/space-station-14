using Robust.Shared.GameObjects;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanEmpEffectSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanEmpEffectComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanEmpEffectComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        _emp.EmpPulse(_transform.GetMapCoordinates(args.Data.HitEntity.Value), hitscan.Comp.Emp.Range, hitscan.Comp.Emp.EnergyConsumption, hitscan.Comp.Emp.DisableDuration);
    }
}
