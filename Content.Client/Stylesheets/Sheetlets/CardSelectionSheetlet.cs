using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class CardSelectionSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    private const string CardModulate = "#121208";

    public override StyleRule[] GetRules(T sheet, object config)
    {
        var headerRect = new StyleBoxTexture
        {
            Texture = ResCache.GetTexture("/Textures/_Starlight/Interface/Nano/card_header.png"),
        };
        headerRect.SetPatchMargin(StyleBox.Margin.Top, 2);
        headerRect.SetPatchMargin(StyleBox.Margin.Bottom, 10);
        headerRect.SetPatchMargin(StyleBox.Margin.Left, 10);
        headerRect.SetPatchMargin(StyleBox.Margin.Right, 7);

        var bannerRect = new StyleBoxTexture
        {
            Texture = ResCache.GetTexture("/Textures/_Starlight/Interface/Nano/card_banner.png"),
        };
        bannerRect.SetPatchMargin(StyleBox.Margin.Top, 8);
        bannerRect.SetPatchMargin(StyleBox.Margin.Bottom, 13);
        bannerRect.SetPatchMargin(StyleBox.Margin.Left, 10);
        bannerRect.SetPatchMargin(StyleBox.Margin.Right, 18);

        var bodyRect = new StyleBoxTexture
        {
            Texture = ResCache.GetTexture("/Textures/_Starlight/Interface/Nano/card_body.png"),
        };
        bodyRect.SetPatchMargin(StyleBox.Margin.All, 3);

        var menuBarRect = new StyleBoxTexture
        {
            Texture = ResCache.GetTexture("/Textures/_Starlight/Interface/Nano/menu.png"),
        };
        menuBarRect.SetPatchMargin(StyleBox.Margin.Top, 5);
        menuBarRect.SetPatchMargin(StyleBox.Margin.Bottom, 5);
        menuBarRect.SetPatchMargin(StyleBox.Margin.Left, 4);
        menuBarRect.SetPatchMargin(StyleBox.Margin.Right, 8);

        var cardBorder = new StyleBoxTexture
        {
            Texture = ResCache.GetTexture("/Textures/_Starlight/Interface/Nano/border.png"),
            Mode = StyleBoxTexture.StretchMode.Stretch,
        };
        cardBorder.SetPatchMargin(StyleBox.Margin.All, 20);

        return
        [
            E<PanelContainer>()
                .Class(CardSelectionStyles.CardHeader)
                .Panel(headerRect)
                .Modulate(Color.FromHex(CardModulate)),
            E<PanelContainer>()
                .Class(CardSelectionStyles.CardBanner)
                .Panel(bannerRect)
                .Modulate(Color.FromHex(CardModulate)),
            E<PanelContainer>()
                .Class(CardSelectionStyles.CardBody)
                .Panel(bodyRect)
                .Modulate(Color.FromHex(CardModulate)),
            E<PanelContainer>()
                .Class(CardSelectionStyles.MenuBar)
                .Panel(menuBarRect)
                .Modulate(Color.FromHex(CardModulate)),
            E<Button>()
                .Class(CardSelectionStyles.CardBorder)
                .Prop(ContainerButton.StylePropertyStyleBox, cardBorder),
        ];
    }
}
