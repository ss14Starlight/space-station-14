using Content.Server.Mech.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._Starlight.Mech.Components;
using Robust.Shared.Random;

namespace Content.Server.Mech.Equipment.EntitySystems;
public sealed class MechGunSystem : EntitySystem
{
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly BatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechInternalWeaponComponent, GunShotEvent>(TryChargeGunBattery);
        SubscribeLocalEvent<MechInternalWeaponComponent, OnEmptyGunShotEvent>(TryChargeGunBatteryEmpty);
    }

    private void TryChargeGunBattery(EntityUid uid, MechInternalWeaponComponent component, ref GunShotEvent args)
    {
        if (HasComp<MechComponent>(args.User)
            && TryComp<BatteryComponent>(uid, out var battery))
            ChargeGunBattery(uid, args.User, battery);
    }

    private void TryChargeGunBatteryEmpty(EntityUid uid, MechInternalWeaponComponent component, ref OnEmptyGunShotEvent args)
    {
        if (HasComp<MechComponent>(args.User)
            && TryComp<BatteryComponent>(uid, out var battery))
            ChargeGunBattery(uid, args.User, battery);
    }

    private void ChargeGunBattery(EntityUid uid, EntityUid mech, BatteryComponent component)
    {
        if (!TryComp<MechInternalWeaponComponent>(uid, out var mechEquipment)
            || !TryComp<MechComponent>(mech, out var chassis))
            return;

        var chargeDelta = component.MaxCharge - component.CurrentCharge;
        // TODO: The battery charge of the mech would be spent directly when fired.
        if (chargeDelta <= 0 
            || chassis.Energy - chargeDelta < 0
            || !_mech.TryChangeEnergy(mech, -chargeDelta, chassis))
            return;

        _battery.SetCharge(uid, component.MaxCharge, component);
    }
}