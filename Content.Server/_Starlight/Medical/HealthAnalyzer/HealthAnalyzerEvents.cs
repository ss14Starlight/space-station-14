using Content.Shared._Starlight.Medical.HealthAnalyzer;

namespace Content.Server._Starlight.Medical.HealthAnalyzer;


/// <summary>
/// Raised when the health analyzer collects additional information to display.
/// </summary>
[ByRefEvent]
public readonly record struct CollectHealthAnalyzerExtensionsEvent()
{
    public readonly List<HealthAnalyzerVitalsBlockData> Vitals = new();
    public readonly List<HealthAnalyzerAbnormalityData> Abnormalities = new();
}
