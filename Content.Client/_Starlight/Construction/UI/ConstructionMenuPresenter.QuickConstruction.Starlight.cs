using Content.Client.UserInterface.Systems.MenuBar.Widgets;

// ReSharper disable CheckNamespace
namespace Content.Client.Construction.UI;

internal sealed partial class ConstructionMenuPresenter
{
    public void OpenFiltered(string searchTerm)
    {
        if (!CraftingAvailable || _constructionView is not ConstructionMenu menu)
            return;

        _selected = null;
        _constructionView.ClearRecipeInfo();
        PopulateCategories(ForAllCategoryName);
        WindowOpen = true;
        _uiManager.GetActiveUIWidget<GameTopMenuBar>().CraftingButton.SetClickPressed(true);
        menu.SetSearchText(searchTerm);
        OnViewPopulateRecipes(_constructionView, (searchTerm, ForAllCategoryName));
    }
}
