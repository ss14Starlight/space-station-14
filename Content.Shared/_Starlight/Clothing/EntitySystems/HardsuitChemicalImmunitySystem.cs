using Content.Shared._Starlight.Chemistry.Events;
using Content.Shared._Starlight.Clothing.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Network;
using InventoryComponent = Content.Shared.Inventory.InventoryComponent;

namespace Content.Shared._Starlight.Clothing.EntitySystems;

/// <summary>
/// System that handles hardsuit chemical immunity, preventing injection-based attacks
/// when wearing hardsuits with the immunity component.
/// </summary>
public sealed class HardsuitChemicalImmunitySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to injection attempt events to check for hardsuit immunity
        // For melee injections (wonderprod)
        SubscribeLocalEvent<InventoryComponent, InjectOnHitAttemptEvent>(OnInventoryMeleeInjectAttempt);
        SubscribeLocalEvent<HardsuitChemicalImmunityComponent, InjectOnHitAttemptEvent>(OnHardsuitMeleeInjectAttempt);

        // For projectile injections (tranquilizer shells)
        SubscribeLocalEvent<InventoryComponent, SolutionInjectAttemptEvent>(OnInventoryProjectileInjectAttempt);
        SubscribeLocalEvent<HardsuitChemicalImmunityComponent, SolutionInjectAttemptEvent>(OnHardsuitProjectileInjectAttempt);

    }

    /// <summary>
    /// Checks if the hardsuit helmet is currently equipped
    /// </summary>
    private bool IsHelmetEquipped(EntityUid hardsuitUid, EntityUid wearerUid)
    {
        // Check if the hardsuit has a toggleable clothing component (helmet system)
        if (!TryComp<ToggleableClothingComponent>(hardsuitUid, out var toggleComp))
            return true; // If no helmet system, assume full protection

        if (toggleComp.ClothingUid == null)
            return true;

        if (toggleComp.Container?.ContainedEntity != null)
            return false; // Helmet is stored in hardsuit, not equipped

        // Double-check by verifying the helmet is actually in the head slot
        return _inventory.TryGetSlotEntity(wearerUid, "head", out var headEntity) &&
               headEntity == toggleComp.ClothingUid;
    }

    // Melee injection handlers (wonderprod)
    private void OnInventoryMeleeInjectAttempt(EntityUid uid, InventoryComponent component, ref InjectOnHitAttemptEvent args)
    {
        // Check the outerClothing slot for hardsuit immunity
        if (_inventory.TryGetSlotEntity(uid, "outerClothing", out var outerClothing, component))
        {
            RaiseLocalEvent(outerClothing.Value, ref args, true);
        }
    }

    private void OnHardsuitMeleeInjectAttempt(Entity<HardsuitChemicalImmunityComponent> ent, ref InjectOnHitAttemptEvent args)
    {
        if (!ent.Comp.Active)
            return;

        var parent = Transform(ent).ParentUid;
        if (!EntityManager.EntityExists(parent))
            return;

        // Check if helmet is equipped - if not, allow injection
        if (!IsHelmetEquipped(ent, parent))
        {
            return;
        }

        // Cancel the injection attempt (helmet is equipped)
        args.Cancelled = true;

        // Show popup message to indicate immunity
        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("hardsuit-chemical-immunity-blocked"),
                parent, parent, PopupType.Small);

            // Show popup to the attacker as well
            if (args.Attacker.HasValue && EntityManager.EntityExists(args.Attacker.Value))
            {
                _popup.PopupEntity(Loc.GetString("hardsuit-chemical-immunity-blocked-attacker"),
                    parent, args.Attacker.Value, PopupType.Small);
            }
        }
    }

    // Projectile injection handlers (tranquilizer shells)
    private void OnInventoryProjectileInjectAttempt(EntityUid uid, InventoryComponent component, ref SolutionInjectAttemptEvent args)
    {
        // Check the outerClothing slot for hardsuit immunity
        if (_inventory.TryGetSlotEntity(uid, "outerClothing", out var outerClothing, component))
        {
            RaiseLocalEvent(outerClothing.Value, ref args, true);
        }
    }

    private void OnHardsuitProjectileInjectAttempt(Entity<HardsuitChemicalImmunityComponent> ent, ref SolutionInjectAttemptEvent args)
    {
        if (!ent.Comp.Active)
            return;

        var parent = Transform(ent).ParentUid;
        if (!EntityManager.EntityExists(parent))
            return;

        // Check if helmet is equipped
        if (!IsHelmetEquipped(ent, parent))
        {
            return;
        }

        // Cancel the injection attempt (helmet is equipped)
        args.Cancelled = true;

        // Show popup message to indicate immunity
        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("hardsuit-chemical-immunity-blocked"),
                parent, parent, PopupType.Small);

            // Show popup to the attacker as well
            if (args.Source.HasValue && EntityManager.EntityExists(args.Source.Value))
            {
                _popup.PopupEntity(Loc.GetString("hardsuit-chemical-immunity-blocked-attacker"),
                    parent, args.Source.Value, PopupType.Small);
            }
        }
    }

}
