using Content.Shared._Funkystation.Stains.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Funkystation.Stains.Systems;

public abstract partial class SharedStainSystem : EntitySystem
{
    /// <summary>
    /// Removes the stains from an item and from any clothing attached to it, such as a hardsuit helmet.
    /// </summary>
    public bool CleanStains(EntityUid item)
    {
        var cleaned = CleanSingleItem(item);

        if (TryComp<ToggleableClothingComponent>(item, out var toggleable) &&
            toggleable.ClothingUid is { } attached)
        {
            cleaned |= CleanSingleItem(attached);
        }

        return cleaned;
    }

    /// <summary>
    /// Removes the stains from every item equipped by an entity.
    /// </summary>
    public void CleanEquippedClothing(EntityUid wearer, InventoryComponent? inventory = null)
    {
        if (!Resolve(wearer, ref inventory, false))
            return;

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory), SlotFlags.WITHOUT_POCKET);
        while (enumerator.NextItem(out var item))
        {
            CleanStains(item);
        }
    }

    private bool CleanSingleItem(EntityUid item)
    {
        if (!TryComp<StainableComponent>(item, out var stain) ||
            !_solution.TryGetSolution(item, stain.SolutionName, out var solution, out var contents) ||
            contents.Volume <= 0)
        {
            return false;
        }

        _solution.RemoveAllSolution(solution.Value);
        UpdateVisuals((item, stain));
        return true;
    }

    private bool HasStains(EntityUid item) => TryComp<StainableComponent>(item, out var stain) &&
                _solution.TryGetSolution(item, stain.SolutionName, out _, out var solution) &&
                solution.Volume > 0;

    private bool AttachedClothingHasStains(EntityUid item) => TryComp<ToggleableClothingComponent>(item, out var toggleable) &&
                toggleable.ClothingUid is { } attached &&
                HasStains(attached);

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
