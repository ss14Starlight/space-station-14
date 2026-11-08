using Content.Shared._Starlight.Light.NightLighting;
using Robust.Client.UserInterface.Controls;

// ReSharper disable once CheckNamespace
namespace Content.Client.Power.APC.UI;

/// <summary>
/// Day/night lighting row of the APC menu.
/// </summary>
public sealed partial class ApcMenu
{
    public event Action<ApcLightingMode>? OnLightingMode;

    private void InitializeLighting()
    {
        var group = new ButtonGroup();
        foreach (var (button, mode) in LightingModeButtons())
        {
            button.Group = group;
            button.OnPressed += _ => OnLightingMode?.Invoke(mode);
        }

        LightingAutoButton.ToolTip = Loc.GetString("apc-menu-lighting-auto-tooltip");
        SetLightingVisible(false);
    }

    /// <summary>
    /// Updates state for current selected light mode, fetches and updates reason for current mode.
    /// </summary>
    public void UpdateLighting(ApcLightingComponent? lighting)
    {
        SetLightingVisible(lighting != null);
        if (lighting == null)
            return;

        SetLightingModePressed(lighting.Mode);

        LightingStateLabel.Text = Loc.GetString((lighting.Night, lighting.Reason) switch
        {
            (false, _) => "apc-menu-lighting-state-day",
            (true, NightLightingReason.Station) => "apc-menu-lighting-state-night-station",
            (true, NightLightingReason.LowPower) => "apc-menu-lighting-state-night-low-power",
            _ => "apc-menu-lighting-state-night-manual",
        });
        LightingStateLabel.SetOnlyStyleClass(lighting.Reason == NightLightingReason.LowPower ? "highlight" : "LabelSubText");
    }

    public void SetLightingModePressed(ApcLightingMode pressed)
    {
        foreach (var (button, mode) in LightingModeButtons())
            button.Pressed = mode == pressed;
    }

    private void SetLightingAccess(bool hasAccess)
    {
        foreach (var (button, _) in LightingModeButtons())
            button.Disabled = !hasAccess;
    }

    private void SetLightingVisible(bool visible)
    {
        LightingLabel.Visible = visible;
        LightingButtons.Visible = visible;
        LightingStateSpacer.Visible = visible;
        LightingStateLabel.Visible = visible;
    }

    private IEnumerable<(Button, ApcLightingMode)> LightingModeButtons()
    {
        yield return (LightingAutoButton, ApcLightingMode.Auto);
        yield return (LightingDayButton, ApcLightingMode.Day);
        yield return (LightingNightButton, ApcLightingMode.Night);
    }
}
