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
        ("CommandButton", Color.FromHex("#1b67a5")),
        ("EngineeringButton", Color.FromHex("#f37700")),
        ("EpistemicsButton", Color.FromHex("#8b308f")), // Science
        ("JusticeButton", Color.FromHex("#326500")), // Law
        ("LogisticsButton", Color.FromHex("#b18644")), // Cargo
        ("MedicalButton", Color.FromHex("#417da2")),
        ("SecurityButton", Color.FromHex("#830000")),
        ("ServiceButton", Color.FromHex("#639137")),
        ("CentralCommandButton", Color.FromHex("#0a5704")),
        ("NanotrasenButton", Color.FromHex("#0d304d")),
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
