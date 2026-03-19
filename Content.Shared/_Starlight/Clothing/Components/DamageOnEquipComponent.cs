using Content.Shared.Damage;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Clothing.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DamageOnEquipComponent : Component
{
    /// <summary>
    /// Damage to do on equip.
    /// </summary>
    [DataField] public DamageSpecifier? EquipDamage;

    /// <summary>
    /// Damage to do on unequip.
    /// </summary>
    [DataField] public DamageSpecifier? UnequipDamage;

    /// <summary>
    /// Which slots to target.
    /// </summary>
    [DataField] public SlotFlags TargetSlots;
    
    /// <summary>
    /// Should damage ignore resistances?
    /// </summary>
    [DataField] public bool IgnoreResistances;
    
    /// <summary>
    /// Should the damage interrupt active doafters?
    /// </summary>
    [DataField] public bool InterruptDoAfters;
    
    /// <summary>
    /// Should the damage ignore global modifiers?
    /// </summary>
    [DataField] public bool IgnoreGlobalModifiers;
    
    /// <summary>
    /// How much armor penetration should apply?
    /// </summary>
    [DataField] public float ArmorPenetration;
    
    /// <summary>
    /// Can negative damage be dealt to heal?
    /// </summary>
    [DataField] public bool CanHeal;
    
    /// <summary>
    /// How long until the damage is applied?
    /// Adding a <see cref="PopupDelay"/> will have that delay added to this.
    /// </summary>
    [DataField] public TimeSpan DamageDelay = TimeSpan.Zero;
    
    /// <summary>
    /// How long until the popup is shown? Will be forced to zero if <see cref="PopupLocId"/> is null.
    /// This delay is added to <see cref="DamageDelay"/>
    /// </summary>
    [DataField] public TimeSpan PopupDelay = TimeSpan.Zero;
    
    /// <summary>
    /// The popup translation to display.
    /// </summary>
    [DataField] public LocId? PopupLocId;
    
    /// <summary>
    /// Should this entity be dropped if the entity holding it dies?
    /// </summary>
    [DataField] public bool DropOnKill;

    /// <summary>
    /// Should this entity be dropped if the entity holding it goes crit?
    /// </summary>
    [DataField] public bool DropOnCrit;
    
    /// <summary>
    /// If <see cref="DropOnKill"/> or <see cref="DropOnCrit"/> are true, should it ignore anything preventing it from being dropped and do it anyway?
    /// </summary>
    [DataField] public bool ForceDrop;
    
    /// <summary>
    /// Should this be able to damage an already dead entity?
    /// </summary>
    [DataField] public bool CanDamageDead;

    /// <summary>
    /// Should this be able to damage an already crit entity?
    /// </summary>
    [DataField] public bool CanDamageCrit;
    
    /// <summary>
    /// List of pending damage events.
    /// </summary>
    [ViewVariables] public readonly List<PendingEquipDamage> PendingEquipDamages = [];
    
    /// <summary>
    /// List of pending popup events.
    /// </summary>
    [ViewVariables] public readonly List<PendingEquipDamagePopup> PendingEquipDamagePopups = [];
    
    /// <summary>
    /// List of players to be ignored on next equip/unequip.
    /// </summary>
    [ViewVariables] public readonly List<EntityUid> IgnoredEntities = [];
}

public record struct PendingEquipDamage(EntityUid Target, EntityUid Damager, TimeSpan Delay, DamageSpecifier Damage);
public record struct PendingEquipDamagePopup(EntityUid Target, EntityUid Damager, TimeSpan Delay, LocId Popup);