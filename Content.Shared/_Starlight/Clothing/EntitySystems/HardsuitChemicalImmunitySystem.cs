using Content.Shared._Starlight.Clothing.Components;
using Content.Shared._Starlight.Combat.OnHit;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;

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
        
        // Subscribe to melee hits to check for hardsuit immunity before injection
        SubscribeLocalEvent<InjectOnHitComponent, MeleeHitEvent>(OnInjectOnMeleeHit, before: new[] { typeof(SharedOnHitSystem) });
    }

    private void OnInjectOnMeleeHit(Entity<InjectOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !args.HitEntities.Any())
            return;

        // Check each target for hardsuit immunity
        foreach (var target in args.HitEntities.ToList())
        {
            // Check if the target is wearing a hardsuit with chemical immunity
            if (_inventory.TryGetSlotEntity(target, "outerClothing", out var outerClothing) &&
                TryComp<HardsuitChemicalImmunityComponent>(outerClothing, out var immunity) &&
                immunity.Active)
            {
                // Remove this target from the hit entities to prevent injection
                args.HitEntities.Remove(target);

                // Show popup message to indicate immunity
                if (_net.IsServer)
                {
                    _popup.PopupEntity(Loc.GetString("hardsuit-chemical-immunity-blocked"), 
                        target, target, PopupType.Medium);
                }
            }
        }
    }
}
