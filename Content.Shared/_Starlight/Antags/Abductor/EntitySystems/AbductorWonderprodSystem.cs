using Content.Shared._Starlight.Antags.Abductor.Components;
using Content.Shared._Starlight.Clothing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Antags.Abductor.EntitySystems;

/// <summary>
/// System that handles abductor wonderprod interactions with hardsuit immunity.
/// When a wonderprod hits someone with hardsuit protection, it reduces its stamina damage to stunbaton levels instead of full wonderprod damage.
/// </summary>
public sealed class AbductorWonderprodSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        // Subscribe to melee hit events from wonderprods to check for hardsuit immunity
        SubscribeLocalEvent<AbductorWonderprodComponent, MeleeHitEvent>(OnWonderprodMeleeHit);
    }

    private void OnWonderprodMeleeHit(Entity<AbductorWonderprodComponent> ent, ref MeleeHitEvent args)
    {
        // Check if the wonderprod has a StaminaDamageOnHit component
        if (!TryComp<StaminaDamageOnHitComponent>(ent, out var staminaDamage))
            return;

        // Store the original damage value
        var originalDamage = staminaDamage.Damage;
        var hasHardsuitTargets = false;

        // Check each target for hardsuit immunity
        foreach (var target in args.HitEntities)
        {
            if (HasHardsuitImmunity(target))
            {
                hasHardsuitTargets = true;
            }
        }

        // If any target has hardsuit immunity, temporarily reduce the damage
        if (hasHardsuitTargets)
        {
            staminaDamage.Damage = ent.Comp.FallbackStaminaDamage;
            
            // The damage will be applied with the reduced value
            // We need to restore the original damage after the hit is processed
            // We'll do this by scheduling a callback
            ent.Owner.SpawnTimer(TimeSpan.Zero, () =>
            {
                if (TryComp<StaminaDamageOnHitComponent>(ent, out var comp))
                {
                    comp.Damage = originalDamage;
                }
            });
        }
    }

    /// <summary>
    /// Checks if the target entity has active hardsuit immunity.
    /// </summary>
    private bool HasHardsuitImmunity(EntityUid target)
    {
        // Check if the target has an inventory
        if (!TryComp<InventoryComponent>(target, out var inventory))
            return false;

        // Check the outerClothing slot for hardsuit immunity
        if (!_inventory.TryGetSlotEntity(target, "outerClothing", out var outerClothing, inventory))
            return false;

        // Check if the outer clothing has hardsuit immunity and it's active
        if (!TryComp<HardsuitChemicalImmunityComponent>(outerClothing.Value, out var immunity))
            return false;

        return immunity.Active;
    }
}
