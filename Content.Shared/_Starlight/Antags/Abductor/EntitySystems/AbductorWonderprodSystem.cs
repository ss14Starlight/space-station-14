using Content.Shared._Starlight.Antags.Abductor.Components;
using Content.Shared._Starlight.Clothing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Starlight.Antags.Abductor.EntitySystems;

/// <summary>
/// System that handles abductor wonderprod interactions with hardsuit immunity.
/// When a wonderprod hits someone with hardsuit protection, it reduces its stamina damage to stunbaton levels instead of full wonderprod damage.
/// </summary>
public sealed class AbductorWonderprodSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbductorWonderprodComponent, StaminaDamageOnHitAttemptEvent>(OnWonderprodStaminaDamageAttempt);
        SubscribeLocalEvent<AbductorWonderprodComponent, MeleeHitEvent>(OnWonderprodMeleeHit);
    }

    private void OnWonderprodStaminaDamageAttempt(Entity<AbductorWonderprodComponent> ent, ref StaminaDamageOnHitAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnWonderprodMeleeHit(Entity<AbductorWonderprodComponent> ent, ref MeleeHitEvent args)
    {
        // Check if the wonderprod has a StaminaDamageOnHit component
        if (!TryComp<StaminaDamageOnHitComponent>(ent, out var staminaDamage))
            return;

        var staminaTargets = new List<EntityUid>();
        foreach (var target in args.HitEntities)
        {
            if (HasComp<StaminaComponent>(target))
                staminaTargets.Add(target);
        }

        if (staminaTargets.Count == 0)
            return;

        var originalDamage = staminaDamage.Damage;

        foreach (var target in staminaTargets)
        {
            var damageToApply = HasHardsuitImmunity(target)
                ? ent.Comp.FallbackStaminaDamage
                : originalDamage;

            _stamina.TakeStaminaDamage(target, damageToApply / staminaTargets.Count,
                source: args.User, with: ent, sound: staminaDamage.Sound);
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
