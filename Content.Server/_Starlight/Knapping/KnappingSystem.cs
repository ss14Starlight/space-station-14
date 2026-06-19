using System.Linq;
using Content.Shared._Starlight.Knapping;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Knapping;

public sealed partial class KnappingSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KnappableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<KnappableComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<KnappableComponent, KnappingTileSetMessage>(OnTileSet);
        SubscribeLocalEvent<KnappableComponent, KnappingRecipeSelectedMessage>(OnRecipeSelected);
        SubscribeLocalEvent<KnappableComponent, KnappingFinishMessage>(OnFinish);
    }

    private void OnMapInit(EntityUid uid, KnappableComponent component, MapInitEvent args)
    {
        EnsureBoard(component);
        EnsureRecipe(component);
    }

    private void OnUiOpened(EntityUid uid, KnappableComponent component, BoundUIOpenedEvent args)
    {
        EnsureBoard(component);
        EnsureRecipe(component);
        UpdateUi(uid, component);
    }

    private void OnTileSet(EntityUid uid, KnappableComponent component, KnappingTileSetMessage args)
    {
        EnsureBoard(component);

        if (args.Filled && !component.AllowRestore)
            return;

        if (!TryGetIndex(component, args.X, args.Y, out var index))
            return;

        if (component.Filled[index] == args.Filled)
            return;

        component.Filled[index] = args.Filled;
        UpdateUi(uid, component);
    }

    private void OnRecipeSelected(EntityUid uid, KnappableComponent component, KnappingRecipeSelectedMessage args)
    {
        if (!component.Recipes.Contains(args.RecipeId))
            return;

        if (!_proto.HasIndex<KnappingRecipePrototype>(args.RecipeId))
            return;

        // Do not reset the board here. The workpiece state belongs to the item, not the recipe dropdown.
        component.SelectedRecipe = args.RecipeId;
        UpdateUi(uid, component);
    }

    private void OnFinish(EntityUid uid, KnappableComponent component, KnappingFinishMessage args)
    {
        EnsureBoard(component);

        if (!TryGetSelectedRecipe(component, out var recipe))
        {
            UpdateUi(uid, component, component.StatusNoRecipe);
            return;
        }

        if (!DoesBoardMatchRecipe(component, recipe))
        {
            UpdateUi(uid, component, component.StatusNotMatched);
            return;
        }

        Spawn(recipe.Output, Transform(uid).Coordinates);
        _popup.PopupEntity(Loc.GetString(component.SuccessPopup, ("recipe", Loc.GetString(recipe.Name))), uid);

        QueueDel(uid);
    }

    private static bool TryGetIndex(KnappableComponent component, int x, int y, out int index)
    {
        index = 0;

        if (x < 0 || y < 0 || x >= component.Width || y >= component.Height)
            return false;

        index = (y * component.Width) + x;
        return index >= 0 && index < component.Filled.Length;
    }

    private void EnsureRecipe(KnappableComponent component)
    {
        if (component.SelectedRecipe != null &&
            component.Recipes.Contains(component.SelectedRecipe) &&
            _proto.HasIndex<KnappingRecipePrototype>(component.SelectedRecipe))
            return;

        component.SelectedRecipe = null;

        foreach (var recipe in component.Recipes)
        {
            if (!_proto.HasIndex<KnappingRecipePrototype>(recipe))
                continue;

            component.SelectedRecipe = recipe;
            return;
        }
    }

    private static void EnsureBoard(KnappableComponent component)
    {
        var size = Math.Max(0, component.Width * component.Height);

        if (component.Filled.Length == size)
            return;

        component.Filled = new bool[size];

        for (var i = 0; i < component.Filled.Length; i++)
            component.Filled[i] = true;
    }

    private bool TryGetSelectedRecipe(KnappableComponent component, out KnappingRecipePrototype recipe)
    {
        recipe = default!;

        var selected = component.SelectedRecipe;
        if (selected == null)
            return false;

        if (!component.Recipes.Contains(selected))
            return false;

        if (!_proto.TryIndex<KnappingRecipePrototype>(selected, out var found) || found == null)
            return false;

        recipe = found;
        return true;
    }

    private void UpdateUi(EntityUid uid, KnappableComponent component, string? status = null)
    {
        var recipes = new List<KnappingRecipeView>();

        foreach (var recipeId in component.Recipes)
        {
            if (!_proto.TryIndex<KnappingRecipePrototype>(recipeId, out var recipe) || recipe == null)
                continue;

            recipes.Add(new KnappingRecipeView(
                recipe.ID,
                recipe.Name,
                recipe.Description,
                recipe.Category,
                recipe.Difficulty,
                recipe.Output,
                recipe.Pattern.ToArray(),
                recipe.AllowOffset));
        }

        var canFinish = TryGetSelectedRecipe(component, out var selectedRecipe) &&
                        DoesBoardMatchRecipe(component, selectedRecipe);

        var text = new KnappingTextSet(
            component.WindowTitle,
            component.WindowSubtitle,
            component.BoardLabel,
            component.RecipeLabel,
            component.PreviewLabel,
            component.WorkpieceHelp,
            component.CarveModeText,
            component.RestoreModeText,
            component.RestoreLockedText,
            component.FinishButtonText,
            component.MaterialLabel);

        _ui.SetUiState(uid,
            KnappingUiKey.Key,
            new KnappingBoundUserInterfaceState(
                component.Width,
                component.Height,
                component.AllowRestore,
                component.MaterialName,
                text,
                component.Filled.ToArray(),
                recipes.ToArray(),
                component.SelectedRecipe,
                canFinish,
                status ?? (canFinish ? component.StatusReady : component.StatusWorking)));
    }

    private static bool DoesBoardMatchRecipe(KnappableComponent component, KnappingRecipePrototype recipe)
    {
        var patternHeight = recipe.Pattern.Count;
        if (patternHeight <= 0)
            return false;

        var patternWidth = recipe.Pattern.Max(x => x.Length);
        if (patternWidth <= 0)
            return false;

        if (patternWidth > component.Width || patternHeight > component.Height)
            return false;

        if (!recipe.AllowOffset)
            return DoesBoardMatchAtOffset(component, recipe, 0, 0, patternWidth, patternHeight);

        for (var yOffset = 0; yOffset <= component.Height - patternHeight; yOffset++)
        {
            for (var xOffset = 0; xOffset <= component.Width - patternWidth; xOffset++)
            {
                if (DoesBoardMatchAtOffset(component, recipe, xOffset, yOffset, patternWidth, patternHeight))
                    return true;
            }
        }

        return false;
    }

    private static bool DoesBoardMatchAtOffset(
        KnappableComponent component,
        KnappingRecipePrototype recipe,
        int xOffset,
        int yOffset,
        int patternWidth,
        int patternHeight)
    {
        for (var y = 0; y < component.Height; y++)
        {
            for (var x = 0; x < component.Width; x++)
            {
                var expected = false;

                var patternX = x - xOffset;
                var patternY = y - yOffset;

                if (patternX >= 0 &&
                    patternY >= 0 &&
                    patternX < patternWidth &&
                    patternY < patternHeight &&
                    patternY < recipe.Pattern.Count &&
                    patternX < recipe.Pattern[patternY].Length)
                {
                    expected = IsFilledPatternCell(recipe.Pattern[patternY][patternX]);
                }

                if (component.Filled[(y * component.Width) + x] != expected)
                    return false;
            }
        }

        return true;
    }

    private static bool IsFilledPatternCell(char c)
        => c is '#' or 'X' or 'x';
}
