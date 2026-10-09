using Content.Client._Starlight.Options.UI;
using Content.Client._Starlight.UserInterface.Controls;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

/// <summary>
/// A sheetlet that provides styles for the flat navigation panel and buttons used in the SectionNav control.
/// </summary>
[StarlightSheetlet]
public sealed class FlatNavSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var proto = theme.Proto;

        return
        [
            E<PanelContainer>()
                .Class(SectionNav.StyleClassNavPanel)
                .Panel(new StyleBoxFlat
                {
                    BackgroundColor = proto.Surface,
                    BorderColor = proto.Border,
                    BorderThickness = new Thickness(0, 0, 1, 0),
                }),

            NavButton().PseudoNormal().Box(Item(proto.Surface, null)).Modulate(Color.White),
            NavButton().PseudoHovered().Box(Item(proto.ElementHovered, null)).Modulate(Color.White),
            NavButton().PseudoPressed().Box(Item(theme.ElementActive, theme.Accent)).Modulate(Color.White),
            NavButton().PseudoDisabled().Box(Item(proto.Surface, null)).Modulate(Color.White),

            E<ContainerButton>()
                .Class(SectionNav.StyleClassNavButton)
                .ParentOf(E<Label>())
                .AlignMode(Label.AlignMode.Left),

            Swatch().PseudoNormal().Box(Frame(Color.Transparent)).Modulate(Color.White),
            Swatch().PseudoHovered().Box(Frame(proto.TextMuted)).Modulate(Color.White),
            Swatch().PseudoPressed().Box(Frame(proto.Text)).Modulate(Color.White),
        ];
    }

    private static MutableSelectorElement NavButton()
        => E<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(SectionNav.StyleClassNavButton);

    private static MutableSelectorElement Swatch()
        => E<ContainerButton>().Class(StyleThemePicker.StyleClassSwatchButton);

    private static StyleBoxFlat Frame(Color border)
        => new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            BorderColor = border,
            BorderThickness = new Thickness(2),
            ContentMarginLeftOverride = 3,
            ContentMarginRightOverride = 3,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };

    private static StyleBoxFlat Item(Color color, Color? marker)
        => new StyleBoxFlat
        {
            BackgroundColor = color,
            BorderColor = marker ?? Color.Transparent,
            BorderThickness = new Thickness(marker == null ? 0 : 2, 0, 0, 0),
            ContentMarginLeftOverride = 12,
            ContentMarginRightOverride = 12,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };
}
