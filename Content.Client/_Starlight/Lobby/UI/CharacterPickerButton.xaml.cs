// ReSharper disable once CheckNamespace
namespace Content.Client.Lobby.UI;

public sealed partial class CharacterPickerButton
{
    private const string EnabledIconPath = "/Textures/Interface/Nano/Monotone/monotone_checkbox_checked.svg.96dpi.png";
    private const string DisabledIconPath = "/Textures/Interface/Nano/Monotone/monotone_checkbox_unchecked.svg.96dpi.png";

    private void UpdateEnabledIcon(bool enabled)
    {
        EnabledIcon.TexturePath = enabled ? EnabledIconPath : DisabledIconPath;
        EnabledCheck.ToolTip = Loc.GetString(enabled ? "character-picker-enabled-tooltip" : "character-picker-disabled-tooltip");
    }
}
