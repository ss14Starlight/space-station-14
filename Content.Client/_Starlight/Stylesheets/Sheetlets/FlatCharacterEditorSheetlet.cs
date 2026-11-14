using Content.Client._Starlight.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

/// <summary>
/// Marking cards of the body tab in the character editor.
/// </summary>
[StarlightSheetlet]
public sealed class FlatCharacterEditorSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var proto = theme.Proto;

        static MutableSelectorElement Card() => E<ContainerButton>().Class(MarkingCard.StyleClassCard);

        return
        [
            Card().PseudoNormal().Box(FlatBox.Bordered(proto.Surface, proto.Border)).Modulate(Color.White),
            Card().PseudoHovered().Box(FlatBox.Bordered(proto.ElementHovered, proto.Border)).Modulate(Color.White),
            Card().PseudoPressed().Box(FlatBox.Bordered(theme.ElementActive, proto.Border)).Modulate(Color.White),
            Card().PseudoDisabled().Box(FlatBox.Bordered(proto.Surface, proto.Border)).Modulate(Color.White.WithAlpha(0.45f)),

            Card().Class(MarkingCard.StyleClassPicked).PseudoNormal().Box(FlatBox.Bordered(proto.Surface, theme.Accent, 2)),
            Card().Class(MarkingCard.StyleClassPicked).PseudoHovered().Box(FlatBox.Bordered(proto.ElementHovered, theme.Accent, 2)),
            Card().Class(MarkingCard.StyleClassPicked).PseudoPressed().Box(FlatBox.Bordered(theme.ElementActive, theme.Accent, 2)),

            // Icons on buttons of the character list, colored like text so they show on light themes too.
            E<TextureRect>().Class("CharacterPickerIcon").Modulate(proto.Text),

            Card().Class(MarkingCard.StyleClassDragged).Modulate(Color.White.WithAlpha(0.5f)),
            Card().Class(MarkingCard.StyleClassDropTarget).Box(FlatBox.Bordered(theme.ElementActive, theme.Accent, 3)),

            E<ContainerButton>()
                .Class(MarkingCard.StyleClassRemove)
                .Box(FlatBox.Fill(Color.White).WithContentMargin(StyleBox.Margin.Horizontal, 3).WithContentMargin(StyleBox.Margin.Vertical, 0)),
            E<ContainerButton>().Class(MarkingCard.StyleClassRemove).PseudoNormal().Modulate(proto.Background.WithAlpha(0.8f)),
            E<ContainerButton>().Class(MarkingCard.StyleClassRemove).PseudoHovered().Modulate(proto.Negative),
            E<ContainerButton>().Class(MarkingCard.StyleClassRemove).PseudoPressed().Modulate(proto.Negative),
        ];
    }
}
