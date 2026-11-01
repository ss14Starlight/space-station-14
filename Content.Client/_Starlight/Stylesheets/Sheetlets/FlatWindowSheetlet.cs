using Content.Client._Starlight.UserInterface;
using Content.Client.Stylesheets;
using Content.Shared._Starlight.UserInterface;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;
using static Content.Client.Stylesheets.StylesheetHelpers;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

[StarlightSheetlet]
public sealed class FlatWindowSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var proto = theme.Proto;

        var windowFallback = FlatBox.BorderedOrChamfered(theme.ClassicDecor, proto.Background, proto.Border, proto.BorderThickness);
        var headerFallback = FlatBox.BorderedOrChamfered(theme.ClassicDecor, proto.Surface, proto.Border, 1)
            .WithContentMargin(StyleBox.Margin.Bottom, 0);

        var windowBox = UseThemeDecor(theme.ClassicDecor, proto.Window)
            ? ThemeBox.Or(proto.Window, ResCache, windowFallback)
            : windowFallback;
        var headerBox = UseThemeDecor(theme.ClassicDecor, proto.Header)
            ? ThemeBox.Or(proto.Header, ResCache, headerFallback)
            : headerFallback;
        var alertHeaderBox = FlatBox.Underlined(theme.Negative.Background, theme.Negative.Base, 1)
            .WithContentMargin(StyleBox.Margin.Bottom, 0);

        var rules = new List<StyleRule>();

        rules.Add(E().Class(DefaultWindow.StyleClassWindowPanel).Panel(windowBox));
        rules.Add(E().Class(DefaultWindow.StyleClassWindowHeader).Panel(headerBox));

        if (!theme.ClassicDecor)
        {
            rules.AddRange([
                E().Class(StyleClass.BorderedWindowPanel).Panel(windowBox),
                E().Class(StyleClass.AlertWindowHeader).Panel(alertHeaderBox),
                E<PanelContainer>().Class("WindowHeadingBackground").Panel(FlatBox.Fill(proto.Surface)),

                Panel(StyleClass.BackgroundPanel, BackgroundBox(proto.Background, proto.Border)),
                Panel(StyleClass.BackgroundPanelDark, BackgroundBox(proto.Surface, proto.Border)),
                Panel(StyleClass.BackgroundPanelOpenLeft, BackgroundBox(proto.Background, proto.Border)
                    .WithContentMargin(StyleBox.Margin.Left, 8)),
                Panel(StyleClass.BackgroundPanelOpenRight, BackgroundBox(proto.Background, proto.Border)
                    .WithContentMargin(StyleBox.Margin.Right, 8)),
                E<PanelContainer>().Class("BackgroundDark").Panel(FlatBox.Fill(proto.Background)),
            ]);
        }

        if (theme.ClassicDecor)
        {
            rules.Add(E<PanelContainer>()
                .Class("BackgroundDark")
                .Panel(FlatBox.Fill(sheet.SecondaryPalette.BackgroundDark)));
        }

        if (proto.WindowBackground is {} windowBackground)
        {
            rules.Add(Child()
                .Parent(E<FancyWindow>())
                .Child(E<PanelContainer>().Class(StyleClass.BackgroundPanel))
                .Prop(PanelContainer.StylePropertyPanel, StyleBoxHelpers.BaseStyleBox(sheet))
                .Modulate(windowBackground));
        }

        rules.AddRange([
            .. ChromeButton(DefaultWindow.StyleClassWindowCloseButton, proto.TextMuted, theme.Negative.Text),
            .. ChromeButton(PopOutExtensions.PopOutButtonStyleClass, proto.TextMuted, proto.Text),
            .. ChromeButton(StarlightStyleClass.WindowSidebarButton, proto.TextMuted, proto.Text),
            .. ChromeButton(FancyWindow.StyleClassWindowHelpButton, proto.TextMuted, proto.Text),

            E<Label>().Class("WindowFooterText").FontColor(proto.TextMuted),
        ]);

        return [.. rules];
    }

    private static readonly ResPath ClassicWindowBackground = new("/Textures/Interface/Nano/window_background.png");
    private static readonly ResPath ClassicWindowHeader = new("/Textures/Interface/Nano/window_header.png");

    private static bool UseThemeDecor(bool classicDecor, StyleThemeBox? decor)
    {
        if (classicDecor || decor == null)
            return true;

        return decor.Texture != ClassicWindowBackground && decor.Texture != ClassicWindowHeader;
    }

    private static StyleRule[] ChromeButton(string styleClass, Color normal, Color hovered)
        =>
        [
            E<TextureButton>().Class(styleClass).PseudoNormal().Modulate(normal),
            E<TextureButton>().Class(styleClass).PseudoHovered().Modulate(hovered),
            E<TextureButton>().Class(styleClass).PseudoPressed().Modulate(hovered.WithAlpha(0.7f)),
        ];

    private static StyleRule Panel(string styleClass, StyleBox box)
        => E().Class(styleClass).Prop(PanelContainer.StylePropertyPanel, box).Modulate(Color.White);

    private static StyleBoxFlat BackgroundBox(Color color, Color border)
        => FlatBox.Bordered(color, border)
            .WithPadding(StyleBox.Margin.All, 1)
            .WithContentMargin(StyleBox.Margin.Vertical, 2)
            .WithContentMargin(StyleBox.Margin.Horizontal, 14);
}
