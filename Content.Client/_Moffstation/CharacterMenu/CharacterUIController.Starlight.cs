// ReSharper disable once CheckNamespace
namespace Content.Client._Moffstation.CharacterMenu;

public sealed partial class CharacterUIController
{
    private void SetAlignment(LocId alignment, Color color)
        => _window!.AlignmentLabel.Text = Loc.GetString("character-info-alignment-format",
            ("color", color.ToHex()),
            ("alignment", Loc.GetString(alignment)));
}
