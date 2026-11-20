using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Traits.UI;

[CommonSheetlet]
public sealed class TraitsSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        // Colors come from the stylesheet palettes, so the tabs follow the picked theme.
        var surfaces = sheet.SecondaryPalette;
        var text = sheet.PrimaryPalette;
        var bgDark = surfaces.BackgroundDark;
        var bgMedium = surfaces.Background;
        var bgLight = surfaces.BackgroundLight;
        var bgLighter = sheet is NanotrasenStylesheet { Theme: { } theme }
            ? theme.Proto.Border
            : text.Element;
        var textPrimary = text.Text;
        var textSecondary = text.TextDark;
        var textMuted = text.TextDark.WithAlpha(0.75f);
        var accentGreen = sheet.PositivePalette.Base;
        var accentYellow = sheet.HighlightPalette.Base.OkBlend(Color.FromHex("#fbbf24"), 0.5f);
        var accentRed = sheet.NegativePalette.Base;
        var accentBlue = sheet.HighlightPalette.Text;

        // StyleBoxes
        var headerPanelBox = new StyleBoxFlat
        {
            BackgroundColor = bgLight,
            BorderColor = bgLighter,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        headerPanelBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var searchBarBox = new StyleBoxFlat { BackgroundColor = bgMedium };
        searchBarBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var searchInputBox = new StyleBoxFlat
        {
            BackgroundColor = bgDark,
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8
        };

        var footerPanelBox = new StyleBoxFlat
        {
            BackgroundColor = bgMedium,
            BorderColor = bgLighter,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };

        var categoryHeaderBox = new StyleBoxFlat { BackgroundColor = bgLight };
        categoryHeaderBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var categoryHeaderButtonBox = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        categoryHeaderButtonBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var categoryContentBox = new StyleBoxFlat { BackgroundColor = bgMedium };

        var categoryAccentBox = new StyleBoxFlat { BackgroundColor = accentBlue };

        var entryPanelBox = new StyleBoxFlat
        {
            BackgroundColor = bgLight,
            BorderColor = bgLighter,
            BorderThickness = new Thickness(1)
        };
        entryPanelBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var entrySelectedBox = new StyleBoxFlat
        {
            BackgroundColor = sheet.HighlightPalette.BackgroundLight,
            BorderColor = accentBlue,
            BorderThickness = new Thickness(1, 1, 1, 1)
        };
        entrySelectedBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var entryDisabledBox = new StyleBoxFlat
        {
            BackgroundColor = bgDark,
            BorderColor = bgLight,
            BorderThickness = new Thickness(1)
        };
        entryDisabledBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var progressBarBgBox = new StyleBoxFlat
        {
            BackgroundColor = bgDark,
            BorderColor = bgLighter,
            BorderThickness = new Thickness(1)
        };

        var progressBarFillFull = new StyleBoxFlat { BackgroundColor = accentGreen };
        var progressBarFillPartial = new StyleBoxFlat { BackgroundColor = accentYellow };
        var progressBarFillLow = new StyleBoxFlat { BackgroundColor = accentRed };
        var progressBarFillEmpty = new StyleBoxFlat { BackgroundColor = bgDark };

        var rules = new List<StyleRule>
        {
            // ===== HEADER PANEL =====
            E<PanelContainer>()
                .Class("TraitsHeaderPanel")
                .Panel(headerPanelBox),

            E<Label>()
                .Class("TraitsTitleLabel")
                .Font(sheet.BaseFont.GetFont(14))
                .FontColor(textPrimary),

            E<Label>()
                .Class("TraitsSubtitleLabel")
                .Font(sheet.BaseFont.GetFont(11))
                .FontColor(textSecondary),

            E<Label>()
                .Class("TraitsStatLabel")
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(accentBlue),

            // ===== PROGRESS BAR =====
            E<PanelContainer>()
                .Class("TraitsProgressBarBg")
                .Panel(progressBarBgBox),

            E<PanelContainer>()
                .Class("TraitsProgressBarFill")
                .Panel(progressBarFillFull),

            E<PanelContainer>()
                .Class("TraitsProgressBarFull")
                .Panel(progressBarFillFull),

            E<PanelContainer>()
                .Class("TraitsProgressBarPartial")
                .Panel(progressBarFillPartial),

            E<PanelContainer>()
                .Class("TraitsProgressBarLow")
                .Panel(progressBarFillLow),

            E<PanelContainer>()
                .Class("TraitsProgressBarEmpty")
                .Panel(progressBarFillEmpty),

            // ===== SEARCH BAR =====
            E<PanelContainer>()
                .Class("TraitsSearchBar")
                .Panel(searchBarBox),

            E<LineEdit>()
                .Class("TraitsSearchInput")
                .Prop(LineEdit.StylePropertyStyleBox, searchInputBox),

            // ===== FOOTER =====
            E<PanelContainer>()
                .Class("TraitsFooterPanel")
                .Panel(footerPanelBox),

            E<Label>()
                .Class("TraitsFooterText")
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(textMuted),

            // ===== CATEGORY HEADER =====
            E<PanelContainer>()
                .Class("TraitsCategoryHeader")
                .Panel(categoryHeaderBox),

            E<Button>()
                .Class("TraitsCategoryHeaderButton")
                .Prop(ContainerButton.StylePropertyStyleBox, categoryHeaderButtonBox),

            E<Label>()
                .Class("TraitsCategoryExpandIcon")
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(textSecondary),

            E<Label>()
                .Class("TraitsCategoryNameLabel")
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(textPrimary),

            E<Label>()
                .Class("TraitsCategoryStatsLabel")
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(textSecondary),

            E<Label>()
                .Class("TraitsCategoryPointsLabel")
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(textMuted),

            // ===== CATEGORY ACCENT =====
            E<PanelContainer>()
                .Class("TraitsCategoryAccent")
                .Panel(categoryAccentBox),

            // ===== CATEGORY CONTENT =====
            E<PanelContainer>()
                .Class("TraitsCategoryContent")
                .Panel(categoryContentBox),

            // ===== TRAIT ENTRY =====
            E<PanelContainer>()
                .Class("TraitsEntryPanel")
                .Panel(entryPanelBox),

            E<PanelContainer>()
                .Class("TraitsEntryPanel", "TraitsEntrySelected")
                .Panel(entrySelectedBox),

            // Disabled entry styling
            E<PanelContainer>()
                .Class("TraitsEntryPanel", "TraitsEntryDisabled")
                .Panel(entryDisabledBox)
                .Modulate(new Color(1f, 1f, 1f, 0.5f)),

            E<Label>()
                .Class("TraitsEntryNameLabel")
                .Font(sheet.BaseFont.GetFont(11))
                .FontColor(textPrimary),

            E<Label>()
                .Class("TraitsEntryCostLabel")
                .Font(sheet.BaseFont.GetFont(11)),

            E<RichTextLabel>()
                .Class("TraitsEntryDescriptionLabel")
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(textSecondary),
        };

        return rules.ToArray();
    }
}
