// ReSharper disable CheckNamespace
using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Clothing.UI;

public sealed record ChameleonMenuOption(string Id, string Name, Func<Control> CreatePreview);

public sealed partial class ChameleonMenu
{
    private IReadOnlyList<ChameleonMenuOption>? _customOptions;
    private string? _customSelectedId;

    public void UpdateOptions(IEnumerable<ChameleonMenuOption> options,
        string? selectedId,
        string title,
        string searchPlaceholder)
    {
        _customOptions = options.ToList();
        _customSelectedId = selectedId;
        Title = title;
        Search.PlaceHolder = searchPlaceholder;
        UpdateGrid();
    }

    public void UpdateReagentState(string? selectedId)
    {
        MinSize = SetSize = new Vector2(300, 350);
        var options = _prototypeManager.EnumeratePrototypes<ReagentPrototype>()
            .Where(reagent => reagent.MetamorphicSprite != null)
            .Select(reagent => new ChameleonMenuOption(reagent.ID, reagent.LocalizedName,
                () => new TextureRect
                {
                    Texture = _sprite.Frame0(reagent.MetamorphicSprite!),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    HorizontalExpand = true,
                    VerticalExpand = true
                }));

        UpdateOptions(options,
            selectedId,
            Loc.GetString("chameleon-glass-ui-title"),
            Loc.GetString("chameleon-glass-ui-search-placeholder"));
    }

    private bool TryUpdateOptionsGrid()
    {
        if (_customOptions == null)
            return false;

        var group = new ButtonGroup();
        foreach (var option in _customOptions)
        {
            if (!option.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) &&
                !option.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var button = new Button
            {
                MinSize = new Vector2(64, 64),
                HorizontalExpand = true,
                Group = group,
                StyleClasses = { StyleClass.ButtonSquare },
                ToggleMode = true,
                Pressed = option.Id == _customSelectedId,
                ToolTip = option.Name
            };
            button.OnPressed += _ => OnIdSelected?.Invoke(option.Id);
            Grid.AddChild(button);
            button.AddChild(option.CreatePreview());
        }

        return true;
    }
}
