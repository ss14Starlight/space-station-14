namespace Content.Shared._Starlight.Knapping;

[RegisterComponent]
public sealed partial class KnappableComponent : Component
{
    [DataField]
    public int Width = 7;

    [DataField]
    public int Height = 7;

    [DataField]
    public bool AllowRestore = true;

    [DataField]
    public string MaterialName = "knapping-material-generic";

    [DataField]
    public List<string> Recipes = [];

    [DataField]
    public string? SelectedRecipe;

    [DataField]
    public bool[] Filled = [];

    [DataField] public string WindowTitle = "knapping-window-title";
    [DataField] public string WindowSubtitle = "knapping-window-subtitle";
    [DataField] public string BoardLabel = "knapping-board-label";
    [DataField] public string RecipeLabel = "knapping-recipes-label";
    [DataField] public string PreviewLabel = "knapping-preview-label";
    [DataField] public string WorkpieceHelp = "knapping-board-help";
    [DataField] public string CarveModeText = "knapping-mode-carve";
    [DataField] public string RestoreModeText = "knapping-mode-restore";
    [DataField] public string RestoreLockedText = "knapping-mode-restore-locked";
    [DataField] public string FinishButtonText = "knapping-finish-button";
    [DataField] public string MaterialLabel = "knapping-material-label";
    [DataField] public string StatusWorking = "knapping-status-working";
    [DataField] public string StatusReady = "knapping-status-ready";
    [DataField] public string StatusNoRecipe = "knapping-status-no-recipe";
    [DataField] public string StatusNotMatched = "knapping-status-not-matched";
    [DataField] public string SuccessPopup = "knapping-popup-success";
}
