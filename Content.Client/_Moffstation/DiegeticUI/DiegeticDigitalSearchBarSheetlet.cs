using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Moffstation.DiegeticUI;

/// <summary>
/// Green-on-black terminal styling for search fields embedded in diegetic machine UIs.
/// </summary>
[CommonSheetlet]
public sealed class DiegeticDigitalSearchBarSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    private const string StyleClassDiegeticSearchBar = "MoffDiegeticSearchBar";
    

    public override StyleRule[] GetRules(T sheet, object config)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = DiegeticDigitalSearchBarSheetlet.ScreenBackground,
            BorderColor = DiegeticDigitalSearchBarSheetlet.ScreenBorder,
            BorderThickness = new Thickness(2),
        };
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 6);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 4);

        return
        [
            E<LineEdit>()
                .Class(StyleClassDiegeticSearchBar)
                .Prop(LineEdit.StylePropertyStyleBox, box)
                .FontColor(DiegeticDigitalSearchBarSheetlet.ScreenText)
                .Prop(LineEdit.StylePropertyCursorColor, DiegeticDigitalSearchBarSheetlet.ScreenText),
            E<LineEdit>()
                .Class(StyleClassDiegeticSearchBar)
                .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .FontColor(DiegeticDigitalSearchBarSheetlet.ScreenTextDim),
        ];
    }
}

static file class DiegeticDigitalSearchBarSheetlet
{
    public static readonly Color ScreenBackground = Color.FromHex("#0a120a");
    public static readonly Color ScreenBorder = Color.FromHex("#1a331a");
    public static readonly Color ScreenText = Color.FromHex("#33ff33");
    public static readonly Color ScreenTextDim = Color.FromHex("#1a551a");
}
