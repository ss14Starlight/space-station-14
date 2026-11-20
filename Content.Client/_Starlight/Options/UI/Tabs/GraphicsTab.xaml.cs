using Content.Client._Starlight.Light;
using Content.Shared._Starlight.CCVar;

// ReSharper disable CheckNamespace
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    private void AddLightingOptions()
    {
        Control.AddOptionPercentSlider(StarlightCCVars.ScreenBrightness, ScreenBrightnessSlider,
            ScreenPostProcessOverlay.MinBrightness, ScreenPostProcessOverlay.MaxBrightness);
        Control.AddOptionPercentSlider(StarlightCCVars.LightBoost, LightBoostSlider,
            LightTonemapOverlay.MinLightBoost, LightTonemapOverlay.MaxLightBoost);

        var grading = Control.AddOptionCheckBox(StarlightCCVars.ColorGrading, ColorGradingCheckBox);
        Control.AddOptionPercentSlider(StarlightCCVars.ColorGradingStrength, ColorGradingStrengthSlider);
        grading.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();

        var gi = Control.AddOptionCheckBox(StarlightCCVars.GlobalIllumination, GlobalIlluminationCheckBox);
        GlobalIlluminationRaysSlider.Slider.Rounded = true;
        Control.AddOption(new OptionSliderIntCVar(Control, _cfg, StarlightCCVars.GlobalIlluminationRays,
            GlobalIlluminationRaysSlider, 4, 32, (_, value) => value.ToString()));
        Control.AddOptionCheckBox(StarlightCCVars.GlobalIlluminationStarlight, GlobalIlluminationStarlightCheckBox);
        Control.AddOptionCheckBox(StarlightCCVars.GlobalIlluminationSurfaceColor, GlobalIlluminationSurfaceColorCheckBox);
        gi.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();

        var nightVision = Control.AddOptionCheckBox(StarlightCCVars.NightVision, NightVisionCheckBox);
        var vignette = Control.AddOptionCheckBox(StarlightCCVars.Vignette, VignetteCheckBox);
        var bloom = Control.AddOptionCheckBox(StarlightCCVars.Bloom, BloomCheckBox);
        var grain = Control.AddOptionCheckBox(StarlightCCVars.FilmGrain, FilmGrainCheckBox);
        var impact = Control.AddOptionCheckBox(StarlightCCVars.ImpactEffects, ImpactEffectsCheckBox);
        Control.AddOptionPercentSlider(StarlightCCVars.ImpactEffectsStrength, ImpactEffectsStrengthSlider);
        var dust = Control.AddOptionCheckBox(StarlightCCVars.DustMotes, DustMotesCheckBox);
        Control.AddOptionPercentSlider(StarlightCCVars.DustMotesStrength, DustMotesStrengthSlider);
        var haze = Control.AddOptionCheckBox(StarlightCCVars.HeatHaze, HeatHazeCheckBox);
        Control.AddOptionPercentSlider(StarlightCCVars.HeatHazeStrength, HeatHazeStrengthSlider);
        var gloss = Control.AddOptionCheckBox(StarlightCCVars.FloorGloss, FloorGlossCheckBox);
        Control.AddOptionPercentSlider(StarlightCCVars.FloorGlossStrength, FloorGlossStrengthSlider);

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
        impact.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
        dust.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
        haze.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
        gloss.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
    }

    private void UpdateLightingSettingsVisibility()
    {
        ColorGradingStrengthSlider.Visible = ColorGradingCheckBox.Pressed;
        GlobalIlluminationRaysSlider.Visible = GlobalIlluminationCheckBox.Pressed;
        GlobalIlluminationStarlightCheckBox.Visible = GlobalIlluminationCheckBox.Pressed;
        GlobalIlluminationSurfaceColorCheckBox.Visible = GlobalIlluminationCheckBox.Pressed;
        NightVisionStrengthSlider.Visible = NightVisionCheckBox.Pressed;
        VignetteStrengthSlider.Visible = VignetteCheckBox.Pressed;
        BloomSettings.Visible = BloomCheckBox.Pressed;
        FilmGrainStrengthSlider.Visible = FilmGrainCheckBox.Pressed;
        ImpactEffectsStrengthSlider.Visible = ImpactEffectsCheckBox.Pressed;
        DustMotesStrengthSlider.Visible = DustMotesCheckBox.Pressed;
        HeatHazeStrengthSlider.Visible = HeatHazeCheckBox.Pressed;
        FloorGlossStrengthSlider.Visible = FloorGlossCheckBox.Pressed;
    }
}
