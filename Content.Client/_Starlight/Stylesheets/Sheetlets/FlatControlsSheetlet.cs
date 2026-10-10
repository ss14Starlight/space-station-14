using Content.Client.Examine;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

[StarlightSheetlet]
public sealed class FlatControlsSheetlet : Sheetlet<NanotrasenStylesheet>
{
    private const int ScrollGrabberSize = 10;

    private const string CheckboxUncheckedPath = "/Textures/_Starlight/Interface/Nano/checkbox_unchecked_white.png";
    private const string CheckboxCheckedPath = "/Textures/_Starlight/Interface/Nano/checkbox_checked_white.png";

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var proto = theme.Proto;
        ISliderConfig sliderCfg = sheet;

        var lineEditBox = FlatBox.Bordered(proto.Background, proto.Border)
            .WithContentMargin(StyleBox.Margin.Vertical, 3)
            .WithContentMargin(StyleBox.Margin.Horizontal, 5);

        var tabActive = FlatBox.Underlined(proto.SurfaceRaised, theme.Accent, 2)
            .WithContentMargin(StyleBox.Margin.Horizontal, 8)
            .WithContentMargin(StyleBox.Margin.Vertical, 3);
        var tabInactive = FlatBox.Fill(proto.Surface)
            .WithContentMargin(StyleBox.Margin.Horizontal, 8)
            .WithContentMargin(StyleBox.Margin.Vertical, 3);

        var tooltipBox = FlatBox.Bordered(proto.Surface.WithAlpha(0.96f), proto.Border, proto.BorderThickness)
            .WithContentMargin(StyleBox.Margin.Vertical, 2)
            .WithContentMargin(StyleBox.Margin.Horizontal, 7);

        var checkboxUnchecked = ResCache.GetTexture(CheckboxUncheckedPath);
        var checkboxChecked = ResCache.GetTexture(CheckboxCheckedPath);

        var sliderFillTex = sheet.GetTextureOr(sliderCfg.SliderFillPath, NanotrasenStylesheet.TextureRoot);
        var sliderBack = SliderBox(sliderFillTex, proto.Background);
        var sliderFill = SliderBox(sliderFillTex, theme.Accent);
        var sliderFore = SliderBox(sheet.GetTextureOr(sliderCfg.SliderOutlinePath, NanotrasenStylesheet.TextureRoot),
            proto.Border);
        var sliderGrab = SliderBox(sheet.GetTextureOr(sliderCfg.SliderGrabber, NanotrasenStylesheet.TextureRoot),
            proto.Text);

        return
        [
            E<Label>().FontColor(proto.Text),
            E<Label>().Class(StyleClass.LabelSubText).FontColor(proto.TextMuted),
            E<Label>().Class(StyleClass.LabelWeak).FontColor(proto.TextMuted),
            E<RichTextLabel>().FontColor(proto.Text),

            E<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox)
                .Prop(TextureRect.StylePropertyTexture, checkboxUnchecked)
                .Modulate(proto.TextMuted),
            E<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox)
                .Class(CheckBox.StyleClassCheckBoxChecked)
                .Prop(TextureRect.StylePropertyTexture, checkboxChecked)
                .Modulate(theme.Accent),

            E<LineEdit>().Prop(LineEdit.StylePropertyStyleBox, lineEditBox),
            E<LineEdit>().Prop("font-color", proto.Text),
            E<LineEdit>().Class(LineEdit.StyleClassLineEditNotEditable).Prop("font-color", proto.TextMuted),
            E<LineEdit>().Pseudo(LineEdit.StylePseudoClassPlaceholder).Prop("font-color", proto.TextDisabled),
            E<TextEdit>().Pseudo(TextEdit.StylePseudoClassPlaceholder).Prop("font-color", proto.TextDisabled),

            E<TabContainer>()
                .Prop(TabContainer.StylePropertyPanelStyleBox, FlatBox.Bordered(proto.Surface, proto.Border))
                .Prop(TabContainer.StylePropertyTabStyleBox, tabActive)
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, tabInactive)
                .Prop(TabContainer.stylePropertyTabFontColor, proto.Text)
                .Prop(TabContainer.StylePropertyTabFontColorInactive, proto.TextMuted),

            E<StripeBack>().Prop(StripeBack.StylePropertyBackground, FlatBox.Fill(proto.Surface)),

            E<ItemList>()
                .Prop(ItemList.StylePropertyBackground, FlatBox.Fill(proto.Background))
                .Prop(ItemList.StylePropertyItemBackground, ListItem(proto.Surface))
                .Prop(ItemList.StylePropertyDisabledItemBackground, ListItem(proto.ElementDisabled))
                .Prop(ItemList.StylePropertySelectedItemBackground, ListItem(theme.ElementActive)),
            E<PanelContainer>()
                .Class(OptionButton.StyleClassOptionsBackground)
                .Panel(FlatBox.Bordered(proto.Surface, proto.Border)),

            E<VScrollBar>().Prop(ScrollBar.StylePropertyGrabber, Grabber(proto.TextMuted.WithAlpha(0.35f), true)),
            E<VScrollBar>().PseudoHovered().Prop(ScrollBar.StylePropertyGrabber, Grabber(proto.TextMuted.WithAlpha(0.55f), true)),
            E<VScrollBar>().PseudoPressed().Prop(ScrollBar.StylePropertyGrabber, Grabber(theme.Accent.WithAlpha(0.6f), true)),
            E<HScrollBar>().Prop(ScrollBar.StylePropertyGrabber, Grabber(proto.TextMuted.WithAlpha(0.35f), false)),
            E<HScrollBar>().PseudoHovered().Prop(ScrollBar.StylePropertyGrabber, Grabber(proto.TextMuted.WithAlpha(0.55f), false)),
            E<HScrollBar>().PseudoPressed().Prop(ScrollBar.StylePropertyGrabber, Grabber(theme.Accent.WithAlpha(0.6f), false)),

            E<Slider>()
                .Prop(Slider.StylePropertyBackground, sliderBack)
                .Prop(Slider.StylePropertyForeground, sliderFore)
                .Prop(Slider.StylePropertyGrabber, sliderGrab)
                .Prop(Slider.StylePropertyFill, sliderFill),

            E<PanelContainer>().Class(StyleClass.TooltipPanel).Panel(tooltipBox).Modulate(Color.White),
            E<Tooltip>().Prop(Tooltip.StylePropertyPanel, tooltipBox),
            E<PanelContainer>().Class(ExamineSystem.StyleClassEntityTooltip).Panel(tooltipBox),
        ];
    }

    private static StyleBoxFlat ListItem(Color color)
        => FlatBox.Fill(color)
            .WithContentMargin(StyleBox.Margin.Horizontal, 4)
            .WithContentMargin(StyleBox.Margin.Vertical, 2);

    private static StyleBoxFlat Grabber(Color color, bool vertical)
    {
        var box = FlatBox.Fill(color).WithContentMargin(StyleBox.Margin.Top, ScrollGrabberSize);
        return vertical ? box.WithContentMargin(StyleBox.Margin.Left, ScrollGrabberSize) : box;
    }

    private static StyleBoxTexture SliderBox(Texture texture, Color modulate)
    {
        var box = new StyleBoxTexture { Texture = texture, Modulate = modulate };
        box.SetPatchMargin(StyleBox.Margin.All, 12);
        return box;
    }
}
