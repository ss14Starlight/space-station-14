using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

[StarlightSheetlet]
public sealed class FlatButtonSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var baseBox = Box(14);
        var openLeftBox = Box(14).WithContentMargin(StyleBox.Margin.Left, 8);
        var openRightBox = Box(14).WithContentMargin(StyleBox.Margin.Right, 8);
        var squareBox = Box(8);

        return
        [
            CButton().Box(baseBox),
            CButton().Class(StyleClass.ButtonOpenLeft).Box(openLeftBox),
            CButton().Class(StyleClass.ButtonOpenRight).Box(openRightBox),
            CButton().Class(StyleClass.ButtonOpenBoth).Box(squareBox),
            CButton().Class(StyleClass.ButtonSquare).Box(squareBox),
            CButton().Class(StyleClass.ButtonSmall).Box(FlatBox.Fill(Color.White)),

            CButton().PseudoDisabled().ParentOf(E<Label>()).FontColor(theme.Proto.TextDisabled),
            CButton().PseudoDisabled().ParentOf(E()).ParentOf(E<Label>()).FontColor(theme.Proto.TextDisabled),
        ];
    }

    private static StyleBoxFlat Box(float horizontalMargin)
    {
        return FlatBox.Fill(Color.White)
            .WithPadding(StyleBox.Margin.All, 1)
            .WithContentMargin(StyleBox.Margin.Vertical, 2)
            .WithContentMargin(StyleBox.Margin.Horizontal, horizontalMargin);
    }

    private static MutableSelectorElement CButton()
    {
        return E<ContainerButton>().Class(ContainerButton.StyleClassButton);
    }
}
