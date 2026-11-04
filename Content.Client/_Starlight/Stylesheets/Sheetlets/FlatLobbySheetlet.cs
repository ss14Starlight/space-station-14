using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.Stylesheets;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

/// <summary>
/// A sheetlet that defines the styles for the lobby interface, including buttons, panels, and job cards, using a flat design aesthetic.
/// </summary>
[StarlightSheetlet]
public sealed class FlatLobbySheetlet : Sheetlet<NanotrasenStylesheet>
{
    private static readonly Color PriorityMediumColor = Color.FromHex("#D4A72C");

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var proto = theme.Proto;
        var opacity = IoCManager.Resolve<IConfigurationManager>().GetCVar(StarlightCCVars.LobbyOpacity);
        var translucent = proto.Background.WithAlpha(Math.Clamp(opacity, 0f, 1f));
        var onAccent = theme.Accent.LabFromSrgb().X > 0.6f ? proto.Background : proto.Text;

        var rules = new List<StyleRule>
        {
            E<PanelContainer>().Class(StarlightStyleClass.LobbyTopBar).Panel(FlatBox.Underlined(translucent, proto.Border, 1)),
            E<PanelContainer>().Class(StarlightStyleClass.LobbyCard).Panel(FlatBox.Bordered(translucent, proto.Border)),

            E<ContainerButton>()
                .Class(StarlightStyleClass.LobbyReadyButton)
                .ParentOf(E<Label>())
                .FontColor(onAccent),

            E<PanelContainer>()
                .Class(StarlightStyleClass.HubPanel)
                .Panel(new StyleBoxFlat
                {
                    BackgroundColor = translucent,
                    BorderColor = theme.Accent,
                    BorderThickness = new Thickness(3, 1, 1, 1),
                }),
            E<PanelContainer>()
                .Class(StarlightStyleClass.HubRow)
                .Panel(FlatBox.Fill(proto.Surface)
                    .WithContentMargin(StyleBox.Margin.Horizontal, 6)
                    .WithContentMargin(StyleBox.Margin.Vertical, 4)),

            E<PanelContainer>().Class(StarlightStyleClass.PriorityBar).Panel(FlatBox.Fill(proto.Border)),
            E<PanelContainer>().Class(StarlightStyleClass.JobDropHighlight).Panel(FlatBox.Fill(theme.ElementActive)),
            E<PanelContainer>().Class(StarlightStyleClass.PriorityHigh).Panel(FlatBox.Fill(proto.Positive)),
            E<PanelContainer>().Class(StarlightStyleClass.PriorityMedium).Panel(FlatBox.Fill(PriorityMediumColor)),
            E<PanelContainer>().Class(StarlightStyleClass.PriorityLow).Panel(FlatBox.Fill(proto.Negative)),
        };

        var bigBox = FlatBox.Fill(Color.White)
            .WithContentMargin(StyleBox.Margin.Horizontal, 16)
            .WithContentMargin(StyleBox.Margin.Vertical, 6);
        rules.AddRange([
            // Top bar
            E<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Class(StarlightStyleClass.LobbyBarButton)
                .Box(FlatBox.Fill(Color.White)
                    .WithContentMargin(StyleBox.Margin.Horizontal, 10)
                    .WithContentMargin(StyleBox.Margin.Vertical, 4)),

            E<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StarlightStyleClass.LobbyReadyButton).Box(bigBox),
            E<ContainerButton>()
                .Class(StarlightStyleClass.LobbyReadyButton)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
            E<ContainerButton>()
                .Class(StarlightStyleClass.LobbyActionButton)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(14)),
        ]);
        ButtonSheetlet<NanotrasenStylesheet>.MakeButtonRules(rules,
            theme.Primary with { Element = Color.Transparent },
            StarlightStyleClass.LobbyBarButton);

        // Observe
        Action(rules, StarlightStyleClass.LobbyActionButton, proto, theme, translucent);

        // Ready
        ButtonSheetlet<NanotrasenStylesheet>.MakeButtonRules(rules,
            theme.Highlight with { PressedElement = theme.Positive.Base },
            StarlightStyleClass.LobbyReadyButton);

        foreach (var styleClass in new[] { StarlightStyleClass.LobbyReadyButton, StarlightStyleClass.LobbyActionButton })
        {
            rules.AddRange([
                E<ContainerButton>()
                    .Class(ContainerButton.StyleClassButton)
                    .Class(styleClass)
                    .PseudoDisabled()
                    .Box(FlatBox.Bordered(translucent, proto.Border)
                        .WithContentMargin(StyleBox.Margin.Horizontal, 16)
                        .WithContentMargin(StyleBox.Margin.Vertical, 6))
                    .Modulate(Color.White),
                E<ContainerButton>()
                    .Class(styleClass)
                    .PseudoDisabled()
                    .ParentOf(E<Label>())
                    .FontColor(proto.TextDisabled),
            ]);
        }

        JobCard(rules, null, proto.Border, proto, theme);
        JobCard(rules, StarlightStyleClass.JobCardHigh, theme.Accent, proto, theme);

        return rules.ToArray();
    }

    private static void Action(List<StyleRule> rules, string styleClass, StyleThemePrototype proto, StyleTheme theme, Color translucent)
    {
        MutableSelectorElement Button() => E<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(styleClass);
        StyleBoxFlat Box(Color color) => FlatBox.Bordered(color, proto.Border)
            .WithContentMargin(StyleBox.Margin.Horizontal, 16)
            .WithContentMargin(StyleBox.Margin.Vertical, 6);

        rules.AddRange([
            Button().PseudoNormal().Box(Box(translucent)).Modulate(Color.White),
            Button().PseudoHovered().Box(Box(proto.ElementHovered)).Modulate(Color.White),
            Button().PseudoPressed().Box(Box(theme.ElementActive)).Modulate(Color.White),
            Button().PseudoDisabled().Box(Box(translucent)).Modulate(Color.White),
        ]);
    }

    private static void JobCard(List<StyleRule> rules, string? extraClass, Color border, StyleThemePrototype proto, StyleTheme theme)
    {
        MutableSelectorElement Card() => E<ContainerButton>().Class(StarlightStyleClass.JobCard).MaybeClass(extraClass);

        rules.AddRange([
            Card().PseudoNormal().Box(FlatBox.Bordered(proto.Surface, border)).Modulate(Color.White),
            Card().PseudoHovered().Box(FlatBox.Bordered(proto.ElementHovered, border)).Modulate(Color.White),
            Card().PseudoPressed().Box(FlatBox.Bordered(theme.ElementActive, border)).Modulate(Color.White),
            Card().PseudoDisabled().Box(FlatBox.Bordered(proto.Background, proto.Border)).Modulate(Color.White),
        ]);
    }
}
