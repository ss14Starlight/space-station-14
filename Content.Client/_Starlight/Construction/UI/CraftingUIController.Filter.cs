// ReSharper disable CheckNamespace
namespace Content.Client.UserInterface.Systems.Crafting;

public sealed partial class CraftingUIController
{
    internal void OpenConstructionMenuFiltered(string searchTerm) => _presenter?.OpenFiltered(searchTerm);
}
