using Content.Shared.Vehicle.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanStunSystem : EntitySystem
{
    [Dependency] private VehicleSystem _rideVehicles = default!; // Starlight
    [Dependency] private SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanStaminaDamageComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanStaminaDamageComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        var target = _rideVehicles.GetExposedOperatorOrSelf(args.Data.HitEntity.Value); // Starlight
        _stamina.TakeStaminaDamage(target, hitscan.Comp.StaminaDamage, source: args.Data.Shooter ?? args.Data.Gun);
    }
}
