using Content.Shared.Clothing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Power;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Clothing;

public abstract class SharedOutlineShieldClothingSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OutlineShieldClothingComponent, ClothingGotEquippedEvent>(OnClothingEquipped);
        SubscribeLocalEvent<OutlineShieldClothingComponent, ClothingGotUnequippedEvent>(OnClothingUnequipped);
        SubscribeLocalEvent<OutlineShieldClothingComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<OutlineShieldClothingComponent, GotUnequippedEvent>(OnUnequipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<OutlineShieldClothingComponent, PowerCellSlotComponent>();
        while (query.MoveNext(out var uid, out var shield, out var powerCell))
        {
            if (!shield.Active || shield.Wearer == null)
                continue;

            // Try to draw power
            if (!_powerCell.TryUseCharge((uid, powerCell), shield.PowerDrawRate * frameTime))
            {
                // Out of power, deactivate shield
                SetShieldActive(uid, false, shield);
            }
        }
    }

    private void OnClothingEquipped(Entity<OutlineShieldClothingComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        Dirty(ent);
    }

    private void OnClothingUnequipped(Entity<OutlineShieldClothingComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        // Deactivate shield when unequipped
        if (ent.Comp.Active)
            SetShieldActive(ent, false, ent.Comp);

        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private void OnUnequipped(Entity<OutlineShieldClothingComponent> ent, ref GotUnequippedEvent args)
    {
        // Also handle regular unequip events (non-clothing slots)
        if (ent.Comp.Active)
            SetShieldActive(ent, false, ent.Comp);

        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private void OnUseInHand(Entity<OutlineShieldClothingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Only allow toggling if it's being worn
        if (ent.Comp.Wearer == null)
            return;

        // Check if we have power
        if (!ent.Comp.Active)
        {
            if (!TryComp<PowerCellSlotComponent>(ent, out var powerCellSlot))
                return;

            if (!_powerCell.HasCharge((ent, powerCellSlot), 0.01f))
                return;
        }

        // Toggle the shield
        SetShieldActive(ent, !ent.Comp.Active, ent.Comp);
        args.Handled = true;
    }

    public virtual void SetShieldActive(EntityUid uid, bool active, OutlineShieldClothingComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Active == active)
            return;

        component.Active = active;
        Dirty(uid, component);
    }
}
