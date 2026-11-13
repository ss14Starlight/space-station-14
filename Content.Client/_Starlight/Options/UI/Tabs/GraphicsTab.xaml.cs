using Content.Shared._Starlight.CCVar;

// ReSharper disable CheckNamespace
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    private void AddLightingOptions()
    {
        var nightVision = Control.AddOptionCheckBox(StarlightCCVars.NightVision, NightVisionCheckBox);
        var vignette = Control.AddOptionCheckBox(StarlightCCVars.Vignette, VignetteCheckBox);
        var bloom = Control.AddOptionCheckBox(StarlightCCVars.Bloom, BloomCheckBox);
        var grain = Control.AddOptionCheckBox(StarlightCCVars.FilmGrain, FilmGrainCheckBox);

        Control.AddOptionPercentSlider(StarlightCCVars.NightVisionStrength, NightVisionStrengthSlider);
        Control.AddOptionPercentSlider(StarlightCCVars.VignetteStrength, VignetteStrengthSlider);
        Control.AddOptionPercentSlider(StarlightCCVars.BloomStrength, BloomStrengthSlider, 0f, 3f);
        Control.AddOptionPercentSlider(StarlightCCVars.BloomThreshold, BloomThresholdSlider, 0f, 0.6f);

        BloomRadiusSlider.Slider.Rounded = true;
        Control.AddOption(new OptionSliderFloatCVar(
            Control,
            _cfg,
            StarlightCCVars.BloomRadius,
            BloomRadiusSlider,
            2f,
            30f,
            1f,
            (_, value) => value.ToString("0")));

        Control.AddOptionPercentSlider(StarlightCCVars.FilmGrainStrength, FilmGrainStrengthSlider, 0f, 1f, 0.2f);

        nightVision.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
        vignette.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
        bloom.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
        grain.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
    }

    private void UpdateLightingSettingsVisibility()
    {
        NightVisionStrengthSlider.Visible = NightVisionCheckBox.Pressed;
        VignetteStrengthSlider.Visible = VignetteCheckBox.Pressed;
        BloomSettings.Visible = BloomCheckBox.Pressed;
        FilmGrainStrengthSlider.Visible = FilmGrainCheckBox.Pressed;
    }
}
