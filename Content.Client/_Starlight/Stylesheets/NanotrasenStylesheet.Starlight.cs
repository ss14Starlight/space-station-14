using Content.Client._Starlight.Stylesheets;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.SheetletConfigs;

// ReSharper disable once CheckNamespace
namespace Content.Client.Stylesheets.Stylesheets;

public partial class NanotrasenStylesheet : IButtonConfig
{
    public StyleTheme? Theme => Config as StyleTheme;

    ColorPalette IButtonConfig.ButtonPalette => Theme?.Primary
        ?? PrimaryPalette with { PressedElement = PositivePalette.PressedElement };
}
