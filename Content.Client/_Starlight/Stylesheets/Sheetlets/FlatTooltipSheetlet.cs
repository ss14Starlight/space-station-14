using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

[StarlightSheetlet]
public sealed class FlatTooltipSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (sheet.Theme is not { } theme)
            return [];

        var tooltipText = theme.Proto.TextTooltip ?? theme.Proto.Text;
        return
        [
            E<RichTextLabel>()
                .Class(StyleClass.TooltipTitle)
                .FontColor(tooltipText),
            E<RichTextLabel>()
                .Class(StyleClass.TooltipDesc)
                .FontColor(tooltipText),
            E<RichTextLabel>()
                .FontColor(tooltipText),
        ];
    }
}
