using Content.Shared._Funkystation.Stains.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Inventory;

namespace Content.Shared._Starlight.Stains.Effects;

/// <summary>
/// Cleans stains from every item equipped by the affected entity.
/// </summary>
public sealed partial class CleanClothingEntityEffectSystem : EntityEffectSystem<InventoryComponent, CleanClothing>
{
    [Dependency] private SharedStainSystem _stains = default!;

    protected override void Effect(Entity<InventoryComponent> entity, ref EntityEffectEvent<CleanClothing> args) => _stains.CleanEquippedClothing(entity.Owner, entity.Comp);
}

public sealed partial class CleanClothing : EntityEffectBase<CleanClothing>;
