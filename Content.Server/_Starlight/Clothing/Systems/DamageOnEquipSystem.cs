using Content.Server.Popups;
using Content.Shared._Starlight.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Clothing.Systems;

public sealed class DamageOnEquipSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageOnEquipComponent, GotEquippedEvent>(DoDamage);
        SubscribeLocalEvent<DamageOnEquipComponent, GotUnequippedEvent>(DoDamage);
    }

    private void DoDamage(EntityUid uid, DamageOnEquipComponent comp, EntityEventArgs ev)
    {
        EntityUid target;
        DamageSpecifier? damage;
        switch (ev)
        {
            case GotEquippedEvent equipped when comp.EquipDamage is not null:
                if (equipped.SlotFlags != comp.TargetSlots) return;
                target = equipped.Equipee;
                damage = comp.EquipDamage;
                break;
            case GotUnequippedEvent unequipped when comp.UnequipDamage is not null:
                if (unequipped.SlotFlags != comp.TargetSlots) return;
                target = unequipped.Equipee;
                damage = comp.UnequipDamage;
                break;
            default: return;
        }
        
        if(comp.Delay is null) _damage.ChangeDamage(target, damage, comp.IgnoreResistances, comp.InterruptDoAfters,
            uid, comp.IgnoreGlobalModifiers, comp.ArmorPenetration, comp.CanHeal);
        else Timer.Spawn(comp.Delay.Value, () =>
        {
            if (Deleted(uid) || Deleted(target)) return;
            _damage.ChangeDamage(target, damage, comp.IgnoreResistances, comp.InterruptDoAfters,
                uid, comp.IgnoreGlobalModifiers, comp.ArmorPenetration, comp.CanHeal);
        });
        
        if(comp.PopupDelay is not null && comp.PopupLocId is not null) Timer.Spawn(comp.PopupDelay.Value, () =>
        {
            if (Deleted(uid) || Deleted(target)) return;
            _popup.PopupEntity(Loc.GetString(comp.PopupLocId), target, type: PopupType.MediumCaution);
        });
    }
}