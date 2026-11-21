using System.Linq;
using System.Numerics;
using System.Text;
using Content.Client._Starlight.UserInterface.Controls;
using Content.Client.Lathe;
using Content.Client.Lathe.UI;
using Content.Client.Materials;
using Content.Client.Materials.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Lathe.UI;

public sealed class LatheWindow : FancyWindow
{
    private static readonly Vector2 _cardSize = new(112, 164);
    private static readonly Color _missingColor = Color.FromHex("#E06666");
    private const int MaxCostLines = 2;

    private readonly IEntityManager _entities;
    private readonly IPrototypeManager _prototypes;
    private readonly IGameTiming _timing;
    private readonly SpriteSystem _sprites;
    private readonly LatheSystem _lathe;
    private readonly MaterialStorageSystem _materials;

    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly BoxContainer _categories = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly LineEdit _amountLine = new() { SetWidth = 56, Text = "1" };
    private readonly Label _count = new() { StyleClasses = { StyleClass.LabelSubText } };
    private readonly WrapContainer _items = new() { SeparationOverride = 6, CrossSeparationOverride = 6, Margin = new Thickness(4) };
    private readonly Button _serverList = new() { Text = Loc.GetString("lathe-menu-server-list"), HorizontalExpand = true };

    private readonly BoxContainer _producing = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Visible = false };
    private readonly BoxContainer _producingIcon = new() { SetSize = new Vector2(32, 32) };
    private readonly Label _producingName = new() { ClipText = true, HorizontalExpand = true, VerticalAlignment = Control.VAlignment.Center };
    private readonly ProgressBar _progress = new() { MinValue = 0, MaxValue = 1, MinHeight = 8, HorizontalExpand = true };
    private readonly BoxContainer _queue = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly Label _queueEmpty = new() { Text = Loc.GetString("lathe-window-queue-empty"), StyleClasses = { StyleClass.LabelSubText } };
    private readonly MaterialStorageControl _materialList = new() { VerticalExpand = true };

    private readonly Dictionary<ProtoId<LatheRecipePrototype>, CatalogCard> _cards = [];
    private readonly Dictionary<ProtoId<LatheCategoryPrototype>, Button> _categoryButtons = [];
    private Button? _allButton;

    private TimeSpan? _productionStart;
    private TimeSpan _productionLength;

    public event Action<BaseButton.ButtonEventArgs>? OnServerListButtonPressed;
    public event Action<string, int>? RecipeQueueAction;
    public event Action<int>? QueueDeleteAction;
    public event Action<int>? QueueMoveUpAction;
    public event Action<int>? QueueMoveDownAction;
    public event Action? DeleteFabricatingAction;

    public List<ProtoId<LatheRecipePrototype>> Recipes = [];
    public ProtoId<LatheCategoryPrototype>? CurrentCategory;
    public EntityUid Entity;

    public LatheWindow()
    {
        IoCManager.Resolve(ref _entities);
        IoCManager.Resolve(ref _prototypes);
        IoCManager.Resolve(ref _timing);
        _sprites = _entities.System<SpriteSystem>();
        _lathe = _entities.System<LatheSystem>();
        _materials = _entities.System<MaterialStorageSystem>();

        Title = Loc.GetString("lathe-menu-title");
        MinSize = new Vector2(700, 440);
        SetSize = new Vector2(1020, 600);

        _search.PlaceHolder = Loc.GetString("lathe-menu-search-designs");
        _search.OnTextChanged += _ => Filter();
        _amountLine.OnTextChanged += _ => OnAmountChanged();
        _serverList.OnPressed += args => OnServerListButtonPressed?.Invoke(args);

        var less = new Button { Text = "−", StyleClasses = { "OpenRight" } };
        var more = new Button { Text = "+", StyleClasses = { "OpenLeft" } };
        less.OnPressed += _ => StepAmount(-1);
        more.OnPressed += _ => StepAmount(1);

        var abort = new Button
        {
            Text = "✖",
            StyleClasses = { "negative" },
            ToolTip = Loc.GetString("lathe-menu-delete-fabricating-tooltip"),
        };
        abort.OnPressed += _ => DeleteFabricatingAction?.Invoke();
        _producing.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children = { _producingIcon, _producingName, abort },
        });
        _producing.AddChild(_progress);

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SetWidth = 170,
            SeparationOverride = 6,
            Children =
            {
                new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _categories } },
                new Label { Text = Loc.GetString("lathe-menu-amount"), StyleClasses = { StyleClass.LabelSubText } },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { less, _amountLine, more },
                },
            },
        };

        var middle = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 6,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children = { _search, _count },
                },
                new PanelContainer
                {
                    StyleClasses = { "InsetPanel" },
                    VerticalExpand = true,
                    Children =
                    {
                        new ScrollContainer
                        {
                            HScrollEnabled = false,
                            VerticalExpand = true,
                            ReserveScrollbarSpace = true,
                            Children = { _items },
                        },
                    },
                },
            },
        };

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SetWidth = 330,
            SeparationOverride = 6,
            Children =
            {
                _serverList,
                Header(Loc.GetString("lathe-menu-queue-title")),
                _producing,
                new ScrollContainer
                {
                    HScrollEnabled = false,
                    VerticalExpand = true,
                    Children = { new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Children = { _queueEmpty, _queue } } },
                },
                Header(Loc.GetString("lathe-menu-materials-title")),
                new PanelContainer { StyleClasses = { "InsetPanel" }, VerticalExpand = true, Children = { _materialList } },
            },
        };

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(4),
            Children = { left, middle, right },
        });
    }

    private static Label Header(string text)
        => new() { Text = text, StyleClasses = { "LabelKeyText" } };

    public void SetEntity(EntityUid uid)
    {
        Entity = uid;
        this.SetInfoFromEntity(_entities, Entity);

        if (_entities.TryGetComponent<LatheComponent>(Entity, out var lathe))
        {
            _serverList.Visible = lathe.DynamicPacks.Count > 0;
            _amountLine.SetText(lathe.DefaultProductionAmount.ToString());
        }

        _materialList.SetOwner(Entity);
    }

    private int _amount
        => int.TryParse(_amountLine.Text, out var amount) && amount > 0 ? amount : 1;

    private void StepAmount(int step)
        => _amountLine.SetText(Math.Clamp(_amount + step, 1, SharedLatheSystem.MaxItemsPerRequest).ToString(), invokeEvent: true);

    private void OnAmountChanged()
    {
        if (int.TryParse(_amountLine.Text, out var amount))
        {
            if (amount > SharedLatheSystem.MaxItemsPerRequest)
                _amountLine.SetText(SharedLatheSystem.MaxItemsPerRequest.ToString());
            else if (amount < 0)
                _amountLine.SetText("0");
        }

        PopulateRecipes();
    }

    /// <summary>
    /// Makes a card for every new recipe, drops cards of recipes no longer offered, and updates what each can afford.
    /// </summary>
    public void PopulateRecipes()
    {
        var offered = Recipes.ToHashSet();
        foreach (var id in _cards.Keys.Where(id => !offered.Contains(id)).ToList())
        {
            _cards[id].Orphan();
            _cards.Remove(id);
        }

        _entities.TryGetComponent(Entity, out LatheComponent? lathe);
        var amount = _amount;
        var added = false;
        foreach (var id in Recipes)
        {
            if (!_prototypes.Resolve(id, out var recipe))
                continue;

            if (!_cards.TryGetValue(id, out var card))
            {
                card = new CatalogCard(_lathe.GetRecipeName(recipe), _cardSize)
                {
                    SearchText = _lathe.GetRecipeDescription(recipe),
                };
                card.SetPreview(RecipePreview(recipe, 2));
                var recipeId = id;
                card.OnPressed += _ => RecipeQueueAction?.Invoke(recipeId, _amount);
                card.TooltipSupplier = _ => new RecipeTooltip(TooltipText(recipe));
                _cards[id] = card;
                added = true;
            }

            var (cost, more) = Cost(recipe, amount, lathe);
            card.SetDetails(cost, more);
            card.SetLocked(!_lathe.CanProduce(Entity, recipe, amount, lathe), showLock: false);
        }

        if (added)
        {
            _items.RemoveAllChildren();
            foreach (var card in _cards.Values.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase))
                _items.AddChild(card);
        }

        Filter();
    }

    private void Filter()
    {
        var search = _search.Text.Trim();
        var shown = 0;
        foreach (var (id, card) in _cards)
        {
            var inCategory = CurrentCategory is not { } category || (_prototypes.Resolve(id, out var recipe) && recipe.Categories.Contains(category));
            card.Visible = inCategory && card.Matches(search);
            if (card.Visible)
                shown++;
        }

        _count.Text = Loc.GetString("lathe-menu-recipe-count", ("count", shown));
    }

    private (List<(string, Color?)> Lines, string? More) Cost(LatheRecipePrototype recipe, int amount, LatheComponent? lathe)
    {
        var multiplier = lathe?.MaterialUseMultiplier ?? 1;
        var lines = new List<(string, Color?)>();
        string? more = null;
        foreach (var (id, perItem) in recipe.Materials)
        {
            if (!_prototypes.Resolve(id, out var material))
                continue;

            if (lines.Count == MaxCostLines)
            {
                more = Loc.GetString("lathe-window-more-materials", ("count", recipe.Materials.Count - MaxCostLines));
                break;
            }

            var needed = SharedLatheSystem.AdjustMaterial(perItem, recipe.ApplyMaterialDiscount, multiplier) * amount;
            var sheets = needed / (float) _materials.GetSheetVolume(material);
            var missing = _materials.GetMaterialAmount(Entity, id) < needed;
            var text = Loc.GetString("lathe-window-material-cost", ("amount", sheets), ("material", Loc.GetString(material.Name)));
            lines.Add((text, missing ? _missingColor : null));
        }

        return (lines, more);
    }

    private string TooltipText(LatheRecipePrototype recipe)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_lathe.GetRecipeName(recipe));
        var multiplier = _entities.TryGetComponent(Entity, out LatheComponent? lathe) ? lathe.MaterialUseMultiplier : 1;

        foreach (var (id, amount) in recipe.Materials)
        {
            if (!_prototypes.Resolve(id, out var material))
                continue;

            var adjusted = SharedLatheSystem.AdjustMaterial(amount, recipe.ApplyMaterialDiscount, multiplier);
            var sheetVolume = _materials.GetSheetVolume(material);
            var unit = Loc.GetString(material.Unit);
            var sheets = adjusted / (float) sheetVolume;
            var missingSheets = Math.Max(0, adjusted - _materials.GetMaterialAmount(Entity, id)) / (float) sheetVolume;
            var name = Loc.GetString(material.Name);

            sb.AppendLine(missingSheets > 0
                ? Loc.GetString("lathe-menu-material-amount-missing", ("amount", sheets), ("missingAmount", missingSheets), ("unit", unit), ("material", name))
                : Loc.GetString("lathe-menu-tooltip-display", ("material", name), ("amount", Loc.GetString("lathe-menu-material-amount", ("amount", sheets), ("unit", unit)))));
        }

        var description = _lathe.GetRecipeDescription(recipe);
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine(Loc.GetString("lathe-menu-description-display", ("description", description)));

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Rebuilds the category list when the recipes bring new categories or lose some.
    /// </summary>
    public void UpdateCategories()
    {
        var present = new HashSet<ProtoId<LatheCategoryPrototype>>();
        foreach (var id in Recipes)
        {
            if (_prototypes.Resolve(id, out var recipe))
                present.UnionWith(recipe.Categories);
        }

        if (_allButton != null && present.SetEquals(_categoryButtons.Keys))
            return;

        _categories.RemoveAllChildren();
        _categoryButtons.Clear();
        _allButton = CategoryButton(Loc.GetString("lathe-menu-category-all"), null);

        foreach (var category in present
                    .Select(id => _prototypes.Index(id))
                    .OrderBy(c => Loc.GetString(c.Name), StringComparer.CurrentCultureIgnoreCase))
        {
            _categoryButtons[category.ID] = CategoryButton(Loc.GetString(category.Name), category.ID);
        }

        if (CurrentCategory is { } current && !present.Contains(current))
            CurrentCategory = null;
        HighlightCategory();
        Filter();
    }

    private Button CategoryButton(string text, ProtoId<LatheCategoryPrototype>? category)
    {
        var button = new Button { Text = text, ToggleMode = true, HorizontalExpand = true, ClipText = true };
        button.Label.Align = Label.AlignMode.Left;
        button.OnPressed += _ =>
        {
            CurrentCategory = category;
            HighlightCategory();
            Filter();
        };
        _categories.AddChild(button);
        return button;
    }

    private void HighlightCategory()
    {
        _allButton?.Pressed = CurrentCategory == null;
        foreach (var (id, button) in _categoryButtons)
            button.Pressed = id == CurrentCategory;
    }

    public void PopulateQueueList(IReadOnlyCollection<LatheRecipeBatch> queue)
    {
        _queue.RemoveAllChildren();
        _queueEmpty.Visible = queue.Count == 0 && !_producing.Visible;

        var index = 0;
        foreach (var batch in queue)
        {
            var recipe = _prototypes.Index(batch.Recipe);
            var name = _lathe.GetRecipeName(batch.Recipe);
            var text = batch.ItemsRequested > 1
                ? Loc.GetString("lathe-menu-item-batch", ("index", index + 1), ("name", name), ("printed", batch.ItemsPrinted), ("total", batch.ItemsRequested))
                : Loc.GetString("lathe-menu-item-single", ("index", index + 1), ("name", name));

            var row = new QueuedRecipeControl(text, index, RecipePreview(recipe, 1));
            row.OnDeletePressed += i => QueueDeleteAction?.Invoke(i);
            row.OnMoveUpPressed += i => QueueMoveUpAction?.Invoke(i);
            row.OnMoveDownPressed += i => QueueMoveDownAction?.Invoke(i);
            _queue.AddChild(row);
            index++;
        }
    }

    public void SetQueueInfo(ProtoId<LatheRecipePrototype>? recipeId)
    {
        _producing.Visible = recipeId != null;
        if (recipeId == null)
            return;

        var recipe = _prototypes.Index(recipeId.Value);
        _producingIcon.RemoveAllChildren();
        _producingIcon.AddChild(RecipePreview(recipe, 1));
        _producingName.Text = _lathe.GetRecipeName(recipe);
        _queueEmpty.Visible = false;
    }

    /// <summary>
    /// Sets the times the progress bar is drawn from. A null start means the lathe waits, for example for materials.
    /// </summary>
    public void SetProgress(TimeSpan? start, TimeSpan length)
    {
        _productionStart = start;
        _productionLength = length;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_producing.Visible)
            return;

        _progress.Value = _productionStart is { } start && _productionLength > TimeSpan.Zero
            ? (float) Math.Clamp((_timing.CurTime - start) / _productionLength, 0, 1)
            : 0;
    }

    private Control RecipePreview(LatheRecipePrototype recipe, float scale)
    {
        if (recipe.Icon != null)
        {
            return new TextureRect
            {
                Texture = _sprites.Frame0(recipe.Icon),
                TextureScale = new Vector2(scale),
                Stretch = TextureRect.StretchMode.KeepCentered,
            };
        }

        if (recipe.Result is { } result)
        {
            var view = new EntityPrototypeView { Scale = new Vector2(scale), SetSize = new Vector2(32 * scale) };
            view.SetPrototype(result);
            return view;
        }

        return new Control();
    }
}
