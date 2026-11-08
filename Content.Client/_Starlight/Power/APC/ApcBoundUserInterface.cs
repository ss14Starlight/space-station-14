using Content.Shared._Starlight.Light.NightLighting;

// ReSharper disable once CheckNamespace
namespace Content.Client.Power.APC;

public sealed partial class ApcBoundUserInterface
{
    /// <summary>
    /// Updates current light mode for opened menu.
    /// </summary>
    public void UpdateLighting()
        => _menu?.UpdateLighting(EntMan.GetComponentOrNull<ApcLightingComponent>(Owner));

    /// <summary>
    /// Sends the requested mode to the server and immediately selects it in the open menu.
    /// </summary>
    private void LightingModePressed(ApcLightingMode mode)
    {
        SendMessage(new ApcSetLightingModeMessage(mode));
        _menu?.SetLightingModePressed(mode);
    }
}
