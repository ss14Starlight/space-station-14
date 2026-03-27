using System.Linq;
using Content.Server.Popups;
using Content.Shared._Starlight.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Clothing.Systems;

public sealed class DamageOnEquipSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _state = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageOnEquipComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<DamageOnEquipComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void DoDamage(EntityUid uid, DamageOnEquipComponent comp, EntityUid target, DamageSpecifier damage)
    {
        if (comp.IgnoredEntities.Remove(target)) return;
        if (comp.PendingEquipDamages.Any(pending => pending.Damager == uid && pending.Target == target)) return;
        if (!comp.CanDamageDead && _state.IsDead(target)) return;
        if (!comp.CanDamageCrit && _state.IsCritical(target)) return;
        var popupDelay = comp.PopupLocId is not null ? comp.PopupDelay : TimeSpan.Zero;
        var damageDelay = comp.PopupDelay + comp.DamageDelay;

        if (popupDelay == TimeSpan.Zero && comp.PopupLocId is not null)
            _popup.PopupEntity(Loc.GetString(comp.PopupLocId), target, target, PopupType.MediumCaution);
        if (damageDelay == TimeSpan.Zero)
        {
            _damage.ChangeDamage(target, damage, comp.IgnoreResistances, comp.InterruptDoAfters,
                target, comp.IgnoreGlobalModifiers, comp.ArmorPenetration, comp.CanHeal);
            if ((comp.DropOnKill && _state.IsDead(target)) || (comp.DropOnCrit && _state.IsCritical(target)))
            {
                if(_inventory.InSlotWithFlags(uid, comp.TargetSlots)) comp.IgnoredEntities.Add(target);
                _container.TryRemoveFromContainer(uid, comp.ForceDrop);
            }
            return;
        }

        if (comp.PopupLocId is { } locId && popupDelay > TimeSpan.Zero) // should be mutually exclusive with the check above
            comp.PendingEquipDamagePopups.Add(new PendingEquipDamagePopup(target, uid, _timing.CurTime + popupDelay, locId));
        comp.PendingEquipDamages.Add(new PendingEquipDamage(target, uid, _timing.CurTime + damageDelay, damage));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<DamageOnEquipComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var pending in comp.PendingEquipDamagePopups.ToList())
            {
                if (Deleted(uid) || Deleted(pending.Target))
                {
                    comp.PendingEquipDamagePopups.Remove(pending);
                    continue;
                }
                if (_timing.CurTime < pending.Delay) continue;
                comp.PendingEquipDamagePopups.Remove(pending);
                _popup.PopupEntity(Loc.GetString(pending.Popup), pending.Target, pending.Target, PopupType.MediumCaution);
            }
            
            foreach (var pending in comp.PendingEquipDamages.ToList())
            {
                if (Deleted(uid) || Deleted(pending.Target))
                {
                    comp.PendingEquipDamages.Remove(pending);
                    continue;
                }
                if(_timing.CurTime < pending.Delay) continue;
                comp.PendingEquipDamages.Remove(pending);
                if (comp.IgnoredEntities.Remove(pending.Target)) continue;
                if (!comp.CanDamageDead && _state.IsDead(pending.Target)) continue;
                if (!comp.CanDamageCrit && _state.IsCritical(pending.Target)) continue;
                _damage.ChangeDamage(pending.Target, pending.Damage, comp.IgnoreResistances, comp.InterruptDoAfters,
                    pending.Target, comp.IgnoreGlobalModifiers, comp.ArmorPenetration, comp.CanHeal);
                if ((comp.DropOnKill && _state.IsDead(pending.Target)) || (comp.DropOnCrit && _state.IsCritical(pending.Target)))
                {
                    if(_inventory.InSlotWithFlags(uid, comp.TargetSlots)) comp.IgnoredEntities.Add(pending.Target);
                    _container.TryRemoveFromContainer(uid, comp.ForceDrop);
                }
            }
        }
    }

    private void OnGotEquipped(EntityUid uid, DamageOnEquipComponent comp, GotEquippedEvent ev)
    {
        if (ev.SlotFlags != comp.TargetSlots) return;
        if (comp.EquipDamage is null) return;
        DoDamage(uid, comp, ev.Equipee, comp.EquipDamage);
    }

    private void OnGotUnequipped(EntityUid uid, DamageOnEquipComponent comp, GotUnequippedEvent ev)
    {
        if (ev.SlotFlags != comp.TargetSlots) return;
        if (comp.UnequipDamage is null) return;
        DoDamage(uid, comp, ev.Equipee, comp.UnequipDamage);
    }
}