using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Starlight.Weapons.Melee;

/// <summary>
/// Starlight: Directly adjusts the breaching hammer's <see cref="ClothingSpeedModifierComponent"/>
/// values when the user equips or unequips a hardsuit, giving a 35% slowdown instead of 40%.
/// Also responds to the hammer being picked up or put down while a hardsuit is already worn.
/// </summary>
public sealed class HammerHardsuitBonusSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ClothingSpeedModifierSystem _clothingSpeedMod = default!;

    // 0.6 = 40% slowdown (base), 0.65 = 35% slowdown (with hardsuit).
    private const float BaseModifier = 0.6f;
    private const float BonusModifier = 0.65f;

    public override void Initialize()
    {
        // Hardsuit equipped/unequipped — find hammer and update its modifier.
        SubscribeLocalEvent<HammerHardsuitBonusComponent, ClothingGotEquippedEvent>(OnHardsuitEquipped);
        SubscribeLocalEvent<HammerHardsuitBonusComponent, ClothingGotUnequippedEvent>(OnHardsuitUnequipped);

        // Hammer picked up/dropped — check if user is wearing a hardsuit.
        SubscribeLocalEvent<BreachingHammerComponent, GotEquippedHandEvent>(OnHammerEquippedHand);
        SubscribeLocalEvent<BreachingHammerComponent, GotUnequippedHandEvent>(OnHammerUnequippedHand);

        // Hammer worn to back/suitStorage slot (or removed from those slots).
        SubscribeLocalEvent<BreachingHammerComponent, ClothingGotEquippedEvent>(OnHammerEquipped);
        SubscribeLocalEvent<BreachingHammerComponent, ClothingGotUnequippedEvent>(OnHammerUnequipped);
    }

    // Hardsuit put on → reduce hammer slowdown to 35%.
    private void OnHardsuitEquipped(Entity<HammerHardsuitBonusComponent> ent, ref ClothingGotEquippedEvent args)
    {
        UpdateHammerModifier(args.Wearer, BonusModifier);
        _movementSpeed.RefreshMovementSpeedModifiers(args.Wearer);
    }

    // Hardsuit taken off → restore full 40% slowdown.
    private void OnHardsuitUnequipped(Entity<HammerHardsuitBonusComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        UpdateHammerModifier(args.Wearer, BaseModifier);
        _movementSpeed.RefreshMovementSpeedModifiers(args.Wearer);
    }

    // Hammer picked up into hand.
    private void OnHammerEquippedHand(Entity<BreachingHammerComponent> ent, ref GotEquippedHandEvent args)
    {
        SetHammerModifier(ent.Owner, IsWearingHardsuit(args.User) ? BonusModifier : BaseModifier);
        _movementSpeed.RefreshMovementSpeedModifiers(args.User);
    }

    // Hammer dropped from hand → reset so it's neutral when next picked up without a hardsuit.
    private void OnHammerUnequippedHand(Entity<BreachingHammerComponent> ent, ref GotUnequippedHandEvent args)
    {
        SetHammerModifier(ent.Owner, BaseModifier);
        _movementSpeed.RefreshMovementSpeedModifiers(args.User);
    }

    // Hammer placed in back/suitStorage slot.
    private void OnHammerEquipped(Entity<BreachingHammerComponent> ent, ref ClothingGotEquippedEvent args)
    {
        SetHammerModifier(ent.Owner, IsWearingHardsuit(args.Wearer) ? BonusModifier : BaseModifier);
        _movementSpeed.RefreshMovementSpeedModifiers(args.Wearer);
    }

    // Hammer removed from back/suitStorage slot → reset modifier.
    private void OnHammerUnequipped(Entity<BreachingHammerComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        SetHammerModifier(ent.Owner, BaseModifier);
        _movementSpeed.RefreshMovementSpeedModifiers(args.Wearer);
    }

    /// <summary>
    /// Returns true if <paramref name="user"/> has a hardsuit (any item with
    /// <see cref="HammerHardsuitBonusComponent"/>) in their outer-clothing slot.
    /// </summary>
    private bool IsWearingHardsuit(EntityUid user)
    {
        var slotEnum = _inventory.GetSlotEnumerator(user, SlotFlags.OUTERCLOTHING);
        while (slotEnum.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } item && HasComp<HammerHardsuitBonusComponent>(item))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Finds breaching hammers in <paramref name="user"/>'s hands and inventory and sets
    /// their <see cref="ClothingSpeedModifierComponent"/> values to <paramref name="modifier"/>.
    /// </summary>
    private void UpdateHammerModifier(EntityUid user, float modifier)
    {
        if (TryComp<HandsComponent>(user, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld((user, hands)))
            {
                if (HasComp<BreachingHammerComponent>(held))
                    SetHammerModifier(held, modifier);
            }
        }

        var slotEnum = _inventory.GetSlotEnumerator(user, SlotFlags.WITHOUT_POCKET);
        while (slotEnum.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } item && HasComp<BreachingHammerComponent>(item))
                SetHammerModifier(item, modifier);
        }
    }

    private void SetHammerModifier(EntityUid hammer, float modifier)
    {
        if (!TryComp<ClothingSpeedModifierComponent>(hammer, out var clothingMod))
            return;

        _clothingSpeedMod.SetWalkSpeedModifier(clothingMod, modifier);
        _clothingSpeedMod.SetSprintSpeedModifier(clothingMod, modifier);
        Dirty(hammer, clothingMod);
    }
}
