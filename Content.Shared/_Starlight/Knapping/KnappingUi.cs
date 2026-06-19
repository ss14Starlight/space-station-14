using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Knapping;

[Serializable, NetSerializable]
public enum KnappingUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class KnappingTextSet(
    string windowTitle,
    string windowSubtitle,
    string boardLabel,
    string recipeLabel,
    string previewLabel,
    string workpieceHelp,
    string carveModeText,
    string restoreModeText,
    string restoreLockedText,
    string finishButtonText,
    string materialLabel)
{
    public string WindowTitle { get; } = windowTitle;
    public string WindowSubtitle { get; } = windowSubtitle;
    public string BoardLabel { get; } = boardLabel;
    public string RecipeLabel { get; } = recipeLabel;
    public string PreviewLabel { get; } = previewLabel;
    public string WorkpieceHelp { get; } = workpieceHelp;
    public string CarveModeText { get; } = carveModeText;
    public string RestoreModeText { get; } = restoreModeText;
    public string RestoreLockedText { get; } = restoreLockedText;
    public string FinishButtonText { get; } = finishButtonText;
    public string MaterialLabel { get; } = materialLabel;
}

[Serializable, NetSerializable]
public sealed class KnappingBoundUserInterfaceState(
    int width,
    int height,
    bool allowRestore,
    string materialName,
    KnappingTextSet text,
    bool[] filled,
    KnappingRecipeView[] recipes,
    string? selectedRecipe,
    bool canFinish,
    string status) : BoundUserInterfaceState
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public bool AllowRestore { get; } = allowRestore;
    public string MaterialName { get; } = materialName;
    public KnappingTextSet Text { get; } = text;
    public bool[] Filled { get; } = filled;
    public KnappingRecipeView[] Recipes { get; } = recipes;
    public string? SelectedRecipe { get; } = selectedRecipe;
    public bool CanFinish { get; } = canFinish;
    public string Status { get; } = status;
}

[Serializable, NetSerializable]
public sealed class KnappingRecipeView(
    string id,
    string name,
    string description,
    string category,
    int difficulty,
    string output,
    string[] pattern,
    bool allowOffset)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    public string Category { get; } = category;
    public int Difficulty { get; } = difficulty;
    public string Output { get; } = output;
    public string[] Pattern { get; } = pattern;
    public bool AllowOffset { get; } = allowOffset;
}

[Serializable, NetSerializable]
public sealed class KnappingTileSetMessage(int x, int y, bool filled) : BoundUserInterfaceMessage
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public bool Filled { get; } = filled;
}

[Serializable, NetSerializable]
public sealed class KnappingRecipeSelectedMessage(string recipeId) : BoundUserInterfaceMessage
{
    public string RecipeId { get; } = recipeId;
}

[Serializable, NetSerializable]
public sealed class KnappingFinishMessage : BoundUserInterfaceMessage
{
}
