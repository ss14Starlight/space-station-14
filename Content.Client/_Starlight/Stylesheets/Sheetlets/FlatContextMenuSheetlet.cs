using Content.Client.ContextMenu.UI;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.SheetletConfigs;
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

        if (theme.ClassicDecor)
        {
            var menuFrame = ThemeBox.Or(proto.Window, ResCache, ClassicMenuBox(sheet));
            var rules = new List<StyleRule>
            {
                E<PanelContainer>()
                    .Class(ContextMenuPopup.StyleClassContextMenuPopup)
                    .Panel(menuFrame),
            };

            var buttonPalette = new ColorPalette(
                Base: proto.Surface,
                LightnessShift: 0f,
                ChromaShift: 0f,
                Element: Color.FromHex("#1119"),
                HoveredElement: proto.ElementHovered,
                PressedElement: theme.ElementActive,
                DisabledElement: proto.ElementDisabled,
                Background: proto.Surface,
                BackgroundLight: proto.SurfaceRaised,
                BackgroundDark: proto.Background,
                Text: proto.Text,
                TextDark: proto.TextMuted);

            ButtonSheetlet<NanotrasenStylesheet>.MakeButtonRules<ContextMenuElement>(rules,
                buttonPalette,
                ContextMenuElement.StyleClassContextMenuButton);

            return rules.ToArray();
        }

        var flatButtonPalette = theme.Primary with { Element = proto.Surface };

        var flatRules = new List<StyleRule>
        {
            E<PanelContainer>()
                .Class(ContextMenuPopup.StyleClassContextMenuPopup)
                .Panel(FlatBox.Bordered(proto.Surface, proto.Border, proto.BorderThickness)
                    .WithContentMargin(StyleBox.Margin.All, ContextMenuElement.ElementMargin)),
        };

        ButtonSheetlet<NanotrasenStylesheet>.MakeButtonRules<ContextMenuElement>(flatRules,
            flatButtonPalette,
            ContextMenuElement.StyleClassContextMenuButton);

        return flatRules.ToArray();
    }

    private static StyleBox ClassicMenuBox(NanotrasenStylesheet sheet)
    {
        IWindowConfig windowCfg = sheet;
        var box = new StyleBoxTexture
        {
            Texture = sheet.GetTextureOr(windowCfg.WindowBackgroundBorderedPath, NanotrasenStylesheet.TextureRoot),
        };
        box.SetPatchMargin(StyleBox.Margin.All, ContextMenuElement.ElementMargin);
        return box;
    }
}
