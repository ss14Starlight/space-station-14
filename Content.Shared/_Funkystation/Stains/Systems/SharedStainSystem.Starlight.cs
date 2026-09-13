using Content.Shared._Funkystation.Stains.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;

namespace Content.Shared._Funkystation.Stains.Systems;

public abstract partial class SharedStainSystem : EntitySystem
{
    /// <summary>
    /// Removes stains from an item and from any clothing attached to it, such as a hardsuit helmet.
    /// When <paramref name="amount"/> is specified, at most that much solution is removed from each item.
    /// </summary>
    public bool CleanStains(Entity<StainableComponent?> item, FixedPoint2? amount = null)
    {
        var cleaned = Resolve(item.Owner, ref item.Comp, false) &&
                        CleanSingleItem((item.Owner, item.Comp), amount);

        if (TryComp<ToggleableClothingComponent>(item.Owner, out var toggleable) &&
            toggleable.ClothingUid is { } attached &&
            TryComp<StainableComponent>(attached, out var attachedStain))
        {
            cleaned |= CleanSingleItem((attached, attachedStain), amount);
        }

        return cleaned;
    }

    /// <summary>
    /// Removes stains from every item equipped by an entity.
    /// When <paramref name="amount"/> is specified, at most that much solution is removed from each item.
    /// </summary>
    public void CleanEquippedClothing(Entity<InventoryComponent?> wearer, FixedPoint2? amount = null)
    {
        if (!Resolve(wearer.Owner, ref wearer.Comp, false))
            return;

        var cleaned = new HashSet<EntityUid>();
        var enumerator = _inventory.GetSlotEnumerator((wearer.Owner, wearer.Comp), SlotFlags.WITHOUT_POCKET);
        while (enumerator.NextItem(out var item))
        {
            if (cleaned.Add(item) && TryComp<StainableComponent>(item, out var stain))
                CleanSingleItem((item, stain), amount);

            if (TryComp<ToggleableClothingComponent>(item, out var toggleable) &&
                toggleable.ClothingUid is { } attached &&
                cleaned.Add(attached) &&
                TryComp<StainableComponent>(attached, out var attachedStain))
            {
                CleanSingleItem((attached, attachedStain), amount);
            }
        }
    }

    private bool CleanSingleItem(Entity<StainableComponent> item, FixedPoint2? amount)
    {
        if (!_solution.TryGetSolution(item.Owner, item.Comp.SolutionName, out var solution, out var contents) ||
            contents.Volume <= 0 ||
            (amount is { } cleanAmount && cleanAmount <= 0))
        {
            return false;
        }

        if (amount is { } quantity)
            _solution.SplitSolution(solution.Value, quantity);
        else
            _solution.RemoveAllSolution(solution.Value);

        UpdateVisuals(item);
        return true;
    }

    public bool HasStains(Entity<StainableComponent?> item)
    {
        if (Resolve(item.Owner, ref item.Comp, false) && SingleItemHasStains((item.Owner, item.Comp)))
            return true;

        return TryComp<ToggleableClothingComponent>(item.Owner, out var toggleable) &&
                toggleable.ClothingUid is { } attached &&
                TryComp<StainableComponent>(attached, out var attachedStain) &&
                SingleItemHasStains((attached, attachedStain));
    }

    private bool SingleItemHasStains(Entity<StainableComponent> item) =>
        _solution.TryGetSolution(item.Owner, item.Comp.SolutionName, out _, out var solution) &&
        solution.Volume > 0;

    private void WringSingleItem(EntityUid item, Solution output)
    {
        if (!TryComp<StainableComponent>(item, out var stain) ||
            !_solution.TryGetSolution(item, stain.SolutionName, out var solutionEntity, out var solution) ||
            solution.Volume <= 0)
        {
            return;
        }

        var split = _solution.SplitSolution(solutionEntity.Value, solution.Volume);
        output.AddSolution(split, _prototype);
        UpdateVisuals((item, stain));
    }
}
