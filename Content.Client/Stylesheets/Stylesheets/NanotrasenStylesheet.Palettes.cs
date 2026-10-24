using Content.Client.Stylesheets.Palette;

namespace Content.Client.Stylesheets.Stylesheets;

public sealed partial class NanotrasenStylesheet
{
    // Starlight-start
    public override ColorPalette PrimaryPalette => Theme?.Primary ?? Palettes.Navy;
    public override ColorPalette SecondaryPalette => Theme?.Secondary ?? Palettes.Slate;
    public override ColorPalette PositivePalette => Theme?.Positive ?? Palettes.Green;
    public override ColorPalette NegativePalette => Theme?.Negative ?? Palettes.Red;
    public override ColorPalette HighlightPalette => Theme?.Highlight ?? Palettes.Gold;
    // Starlight-end
}
