using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Starlight.AACTablet.UI;

[CommonSheetlet]
public sealed class AACSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    private static readonly (string StyleClass, Color Department)[] SubjectButtonColors =
    [
        ("CommandButton", Color.FromHex("#fcdf03")),
        ("EngineeringButton", Color.FromHex("#ff733c")),
        ("EpistemicsButton", Color.FromHex("#cd7ccd")), // Science
        ("JusticeButton", Color.FromHex("#396901")), // Law
        ("LogisticsButton", Color.FromHex("#b48b57")), // Cargo
        ("MedicalButton", Color.FromHex("#57b8f0")),
        ("SecurityButton", Color.FromHex("#ff4242")),
        ("ServiceButton", Color.FromHex("#539c00")),
        ("CentralCommandButton", Color.FromHex("#00b700")),
        ("NanotrasenButton", Color.FromHex("#2253b5")),
    ];

    public override StyleRule[] GetRules(T sheet, object config)
    {
        var rules = new List<StyleRule>
        {
            E<RichTextLabel>()
                .Class("WhiteText")
                .FontColor(Color.White),
            E<Label>()
                .Class("WhiteText")
                .FontColor(Color.White),
        };

        foreach (var (styleClass, department) in SubjectButtonColors)
        {
            var hover = Color.InterpolateBetween(department, Color.White, 0.2f);
            var pressed = Color.InterpolateBetween(department, Color.Black, 0.2f);

            rules.AddRange([
                E<ContainerButton>()
                    .Class(ContainerButton.StyleClassButton)
                    .Class(styleClass)
                    .PseudoNormal()
                    .Prop(Control.StylePropertyModulateSelf, department),
                E<ContainerButton>()
                    .Class(ContainerButton.StyleClassButton)
                    .Class(styleClass)
                    .PseudoHovered()
                    .Prop(Control.StylePropertyModulateSelf, hover),
                E<ContainerButton>()
                    .Class(ContainerButton.StyleClassButton)
                    .Class(styleClass)
                    .PseudoPressed()
                    .Prop(Control.StylePropertyModulateSelf, pressed),
            ]);
        }

        return rules.ToArray();
    }
}
