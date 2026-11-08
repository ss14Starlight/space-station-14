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

    /// <summary>
    /// Groups the lighting mode buttons, connects their events, and initially hides lighting controls.
    /// </summary>
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
    /// Updates the selected mode and status reason, highlighting low power mode. Hides lighting controls when lighting is null.
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

    /// <summary>
    /// Selects the button matching the mode and clears the others.
    /// </summary>
    public void SetLightingModePressed(ApcLightingMode pressed)
    {
        foreach (var (button, mode) in LightingModeButtons())
            button.Pressed = mode == pressed;
    }

    /// <summary>
    /// Enables or disables lighting mode buttons according to access.
    /// </summary>
    private void SetLightingAccess(bool hasAccess)
    {
        foreach (var (button, _) in LightingModeButtons())
            button.Disabled = !hasAccess;
    }

    /// <summary>
    /// Shows or hides the lighting mode controls and status label.
    /// </summary>
    private void SetLightingVisible(bool visible)
    {
        LightingLabel.Visible = visible;
        LightingButtons.Visible = visible;
        LightingStateSpacer.Visible = visible;
        LightingStateLabel.Visible = visible;
    }

    /// <summary>
    /// Yields the automatic, day, and night mode buttons with their corresponding modes.
    /// </summary>
    private IEnumerable<(Button, ApcLightingMode)> LightingModeButtons()
    {
        yield return (LightingAutoButton, ApcLightingMode.Auto);
        yield return (LightingDayButton, ApcLightingMode.Day);
        yield return (LightingNightButton, ApcLightingMode.Night);
    }
}
