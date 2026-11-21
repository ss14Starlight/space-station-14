using Content.Client.UserInterface.Systems.Crafting;
using Content.Shared.Construction.Prototypes;
using Robust.Client.UserInterface;

// ReSharper disable CheckNamespace
namespace Content.Client._DEN.QuickConstruction.UI;

public sealed partial class QuickConstructionBoundUserInterface
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    private void HandleAlternativePlacement(ConstructionPrototype prototype, string targetName)
    {
        if (prototype.Type == ConstructionType.Item)
        {
            HandlePlacement(prototype);
            return;
        }

        var searchTerm = string.IsNullOrWhiteSpace(prototype.Name) ? targetName : prototype.Name;
        _uiManager.GetUIController<CraftingUIController>().OpenConstructionMenuFiltered(searchTerm);
    }
}
