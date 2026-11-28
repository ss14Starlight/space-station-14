using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Stylesheets.Sheetlets;

[StarlightSheetlet]
public sealed class AACButtonSheetlet : Sheetlet<NanotrasenStylesheet>
{
    private static readonly (string StyleClass, ColorPalette Palette)[] _departmentButtons =
    [
        ("CommandButton", ColorPalette.FromHexBase("#8c7a2a")),
        ("EngineeringButton", ColorPalette.FromHexBase("#a35a2c")),
        ("EpistemicsButton", ColorPalette.FromHexBase("#7a4c84")),
        ("JusticeButton", Palettes.Navy),
        ("LogisticsButton", ColorPalette.FromHexBase("#86663d")),
        ("MedicalButton", ColorPalette.FromHexBase("#33739c")),
        ("SecurityButton", Palettes.Red),
        ("ServiceButton", Palettes.Green),
    ];

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        var rules = new List<StyleRule>();

        foreach (var (styleClass, palette) in _departmentButtons)
            ButtonSheetlet<NanotrasenStylesheet>.MakeButtonRules(rules, palette, styleClass);

        return [.. rules];
    }
}
