using Content.Shared.MedicalScanner;
using Robust.Client.Graphics;

namespace Content.Client._Starlight.HealthAnalyzer.UI;

public struct HealthAnalyzerVitalsBlockData
{
    public string Name;
    public string Value;

    public bool HasBar;
    public float BarRatio;

    public Color ValueColor;
    public Color BarColor;

    public bool HasIcon;
    public Texture IconTexture;
}

/// <summary>
/// Raised when the health analyzer collects additional blocks to display in the Vitals Overview section
/// </summary>
[ByRefEvent]
public readonly record struct CollectHealthAnalyzerVitalsEvent(
    EntityUid Target,
    HealthAnalyzerUiState State)
{
    public readonly List<HealthAnalyzerVitalsBlockData> Vitals = new();
}

/// <summary>
/// Raised when the health analyzer collects additional information to display in the abnormalities section
/// </summary>
[ByRefEvent]
public readonly record struct CollectHealthAnalyzerAbnormalitiesEvent(
    EntityUid Target,
    HealthAnalyzerUiState State)
{
    public readonly List<HealthAnalyzerVitalsBlockData> Vitals = new();
}
