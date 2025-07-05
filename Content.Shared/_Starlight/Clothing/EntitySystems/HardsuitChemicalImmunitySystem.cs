using Content.Shared._Starlight.Chemistry.Events;
using Content.Shared._Starlight.Clothing.Components;
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

        // Cancel the injection attempt
        args.Cancelled = true;

        // Show popup message to indicate immunity
        if (_net.IsServer)
        {
            // Find the entity wearing this hardsuit by checking the parent container
            var parent = Transform(ent).ParentUid;
            if (EntityManager.EntityExists(parent))
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

        // Cancel the injection attempt
        args.Cancelled = true;

        // Show popup message to indicate immunity
        if (_net.IsServer)
        {
            // Find the entity wearing this hardsuit by checking the parent container
            var parent = Transform(ent).ParentUid;
            if (EntityManager.EntityExists(parent))
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

}
