using Content.Shared._Starlight.Light.NightLighting;

// ReSharper disable once CheckNamespace
namespace Content.Client.Power.APC;

public sealed partial class ApcBoundUserInterface
{
    public void UpdateLighting()
    {
        _menu?.UpdateLighting(EntMan.GetComponentOrNull<ApcLightingComponent>(Owner));
    }

    private void LightingModePressed(ApcLightingMode mode)
    {
        SendMessage(new ApcSetLightingModeMessage(mode));
        _menu?.SetLightingModePressed(mode);
    }
}
