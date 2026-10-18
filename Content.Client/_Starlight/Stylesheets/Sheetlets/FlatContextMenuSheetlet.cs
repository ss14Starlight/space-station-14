using Content.Client.ContextMenu.UI;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

/// <summary>
/// The right-click context menu. It opens over the game but lives in the popup root, so it follows the main theme.
/// </summary>
[StarlightSheetlet]
public sealed class FlatContextMenuSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var proto = theme.Proto;
        var buttonPalette = theme.Primary with { Element = proto.Surface };

        var rules = new List<StyleRule>
        {
            E<PanelContainer>()
                .Class(ContextMenuPopup.StyleClassContextMenuPopup)
                .Panel(FlatBox.Bordered(proto.Surface, proto.Border, proto.BorderThickness)
                    .WithContentMargin(StyleBox.Margin.All, ContextMenuElement.ElementMargin)),
        };

        ButtonSheetlet<NanotrasenStylesheet>.MakeButtonRules<ContextMenuElement>(rules,
            buttonPalette,
            ContextMenuElement.StyleClassContextMenuButton);

        return rules.ToArray();
    }
}
