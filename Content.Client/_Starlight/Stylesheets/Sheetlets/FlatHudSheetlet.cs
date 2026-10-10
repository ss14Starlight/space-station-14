using Content.Client._Starlight.UserInterface.Controls;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

/// <summary>
/// A sheetlet that applies a flat, modern style to the HUD, including menu buttons and chat controls.
/// </summary>
[StarlightSheetlet]
public sealed class FlatHudSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var proto = theme.Proto;
        var menuButtonBox = FlatBox.Fill(Color.White).WithPadding(StyleBox.Margin.All, 1);
        var menuItemBox = FlatBox.Fill(Color.White)
            .WithContentMargin(StyleBox.Margin.Horizontal, 6)
            .WithContentMargin(StyleBox.Margin.Vertical, 2);
        var chatButtonBox = FlatBox.Fill(Color.White)
            .WithContentMargin(StyleBox.Margin.Horizontal, 6)
            .WithContentMargin(StyleBox.Margin.Vertical, 2);

        var rules = new List<StyleRule>
        {
            // Menu bar
            E<MenuButton>().Box(menuButtonBox),
            E<MenuButton>().Class(StyleClass.ButtonSquare).Box(menuButtonBox),
            E<MenuButton>().Class(StyleClass.ButtonOpenLeft).Box(menuButtonBox),
            E<MenuButton>().Class(StyleClass.ButtonOpenRight).Box(menuButtonBox),
            E<MenuButton>().Class(StyleClass.ButtonOpenBoth).Box(menuButtonBox),
            E<MenuButton>().Class(DropdownButton.StyleClassDropdownItem).Box(menuItemBox),
            E<Label>()
                .Class(MenuButton.StyleClassLabelTopButton)
                .Font(sheet.BaseFont.GetFont(9, FontKind.Bold)),

            // Chat
            E<PanelContainer>().Class(ChatInputBox.StyleClassChatPanel).Panel(FlatBox.Fill(proto.Background)),
            E<ChatInputBox>().Class(ChatInputBox.StyleClassChatPanel).Panel(new StyleBoxEmpty()),
            E().Class(SeparatedChatGameScreen.StyleClassChatContainer).Panel(FlatBox.Fill(proto.Surface)),
            E<LineEdit>()
                .Class(ChatInputBox.StyleClassChatLineEdit)
                .Prop(LineEdit.StylePropertyStyleBox, FlatBox.Bordered(proto.Surface, proto.Border)
                    .WithContentMargin(StyleBox.Margin.Horizontal, 6)
                    .WithContentMargin(StyleBox.Margin.Vertical, 3)),
            E<ContainerButton>().Class(ChatInputBox.StyleClassChatFilterOptionButton).Box(chatButtonBox),
            E<ContainerButton>().Class(ChannelSelectorItemButton.StyleClassChatSelectorOptionButton).Box(chatButtonBox),
        };

        IconColors<MenuButton>(rules, theme);
        IconColors<ChannelFilterButton>(rules, theme);

        return rules.ToArray();
    }

    private static void IconColors<T>(List<StyleRule> rules, StyleTheme theme) where T : Control
        => rules.AddRange([
            E<T>().PseudoNormal().Prop(StarlightStyleProperty.IconColor, theme.Proto.TextMuted),
            E<T>().PseudoHovered().Prop(StarlightStyleProperty.IconColor, theme.Proto.Text),
            E<T>().PseudoPressed().Prop(StarlightStyleProperty.IconColor, theme.Accent),
            E<T>().PseudoDisabled().Prop(StarlightStyleProperty.IconColor, theme.Proto.TextDisabled),
        ]);
}
