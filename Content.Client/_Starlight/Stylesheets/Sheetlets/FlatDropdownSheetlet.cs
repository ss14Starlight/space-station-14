using Content.Client._Starlight.UserInterface.Controls;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

/// <summary>
/// A sheetlet that applies a flat, modern style to dropdown buttons and their items.
/// </summary>
[StarlightSheetlet]
public sealed class FlatDropdownSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var proto = theme.Proto;
        var itemBox = FlatBox.Fill(Color.White)
            .WithContentMargin(StyleBox.Margin.Horizontal, 8)
            .WithContentMargin(StyleBox.Margin.Vertical, 3);

        var rules = new List<StyleRule>
        {
            E<PanelContainer>()
                .Class(DropdownButton.StyleClassDropdownPanel)
                .Panel(FlatBox.Bordered(proto.Surface, proto.Border).WithContentMargin(StyleBox.Margin.All, 2)),
            E<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Class(DropdownButton.StyleClassDropdownItem)
                .Box(itemBox),
            E<ContainerButton>()
                .Class(DropdownButton.StyleClassDropdownItem)
                .ParentOf(E<Label>())
                .AlignMode(Label.AlignMode.Left),
        };

        ButtonSheetlet<NanotrasenStylesheet>.MakeButtonRules(rules,
            theme.Primary with { Element = proto.Surface },
            DropdownButton.StyleClassDropdownItem);

        return rules.ToArray();
    }
}
