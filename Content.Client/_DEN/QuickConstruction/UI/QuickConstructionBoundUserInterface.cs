using System.Linq;
using Content.Client.Construction;
using Content.Client.UserInterface.Controls;
using Content.Shared._DEN.QuickConstruction.Components;
using Content.Shared._DEN.QuickConstruction.Prototypes;
using Content.Shared.Construction.Prototypes;
using JetBrains.Annotations;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._DEN.QuickConstruction.UI;
[UsedImplicitly]
public sealed class QuickConstructionBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlacementManager _placementMan = default!;

    private SimpleRadialMenu? _menu;

    public QuickConstructionBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) =>
        IoCManager.InjectDependencies(this);
    //SL - warning fix

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<QuickConstructableComponent>(Owner, out var quickConstructable)
            || !_prototypeManager.TryIndex(quickConstructable.Category, out var prototype))
            return;

        var models = ConvertToButtons(prototype.ConstructionEntries, prototype.CategoryEntries);

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(models);
        _menu.OpenOverMouseScreenPosition();
    }

    // Starlight Edit Start
    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(
        List<ProtoId<ConstructionPrototype>> constructionEntries,
        List<ProtoId<QuickConstructionCategoryPrototype>> categoryEntries,
        HashSet<ProtoId<QuickConstructionCategoryPrototype>>? visitedCategories = null)
    {
        visitedCategories ??= [];
        var constructionSystem = EntMan.System<ConstructionSystem>();

        ValueList<RadialMenuActionOptionBase> constructionButtons = [];
        Dictionary<QuickConstructionCategoryPrototype, List<RadialMenuOptionBase>> categoryButtons = [];

        foreach (var constructionEntry in constructionEntries)
        {
            if (!_prototypeManager.TryIndex(constructionEntry, out var prototype)
                || !constructionSystem.TryGetRecipePrototype(constructionEntry, out var recipePrototypeId)
                || !_prototypeManager.TryIndex(recipePrototypeId, out var recipePrototype))
                continue;

            var topLevelActionOption = new RadialMenuActionOption<ConstructionPrototype>(HandlePlacement, prototype)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(recipePrototype),
                    ToolTip = recipePrototype.Name,
                };
            constructionButtons.Add(topLevelActionOption);
        }

        foreach (var categoryEntry in categoryEntries.Where(categoryEntry => visitedCategories.Add(categoryEntry)))
        {
            if (!_prototypeManager.TryIndex(categoryEntry, out var prototype) || categoryButtons.TryGetValue(prototype, out var list))
            {
                visitedCategories.Remove(categoryEntry);
                continue;
            }

            list = ConvertToButtons(prototype.ConstructionEntries, prototype.CategoryEntries, visitedCategories).ToList();
            categoryButtons.Add(prototype, list);
            visitedCategories.Remove(categoryEntry);
        }
        // Starlight Edit End

        var models =
            new RadialMenuOptionBase[constructionButtons.Count +
                                     categoryButtons.Count]; // SL - fixed using input count instead of current count
        var modelIndex = 0;

        foreach (var (prototype, buttonList) in categoryButtons)
        {
            models[modelIndex] = new RadialMenuNestedLayerOption(buttonList)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(prototype.Icon),
                ToolTip = Loc.GetString(prototype.Name),
            };
            modelIndex++;
        }

        foreach (var action in constructionButtons)
        {
            models[modelIndex] = action;
            modelIndex++;
        }

        return models;
    }

    private void HandlePlacement(ConstructionPrototype proto)
    {
        var constructionSystem = EntMan.System<ConstructionSystem>();

        if (proto.Type == ConstructionType.Item)
        {
            constructionSystem.TryStartItemConstruction(proto.ID);
            return;
        }

        _placementMan.BeginPlacing(new PlacementInformation
            {
                IsTile = false,
                PlacementOption = proto.PlacementMode,
            },
            new ConstructionPlacementHijack(constructionSystem, proto));

        //_menu?.Close(); //SL - I think this was trying to keep the menu open when crafting items, but the radial system calls menu.close anyway and i dont wanna edit all that
    }
}
