using Content.Shared._FarHorizons.Vehicles.Components; //FarHorizons
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanStunSystem : EntitySystem
{
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

        //FarHorizons-edit Start
        var target = args.Data.HitEntity.Value;
        if(TryComp<VehicleComponent>(target, out var vehicle) && HasComp<VehicleBuckleComponent>(target) && vehicle.Rider != null)
            target = vehicle.Rider.Value;

        _stamina.TakeStaminaDamage(target, hitscan.Comp.StaminaDamage, source: args.Data.Shooter ?? args.Data.Gun);
        //FarHorizons-edit End
    }
}
