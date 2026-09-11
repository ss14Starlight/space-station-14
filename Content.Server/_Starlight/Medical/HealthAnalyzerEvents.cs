using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared.MedicalScanner;

namespace Content.Server._Starlight.Medical;

/// <summary>
/// Raised when the health analyzer collects additional blocks to display in the Vitals Overview section
/// </summary>
[ByRefEvent]
public readonly record struct CollectHealthAnalyzerVitalsEvent()
{
    public readonly List<HealthAnalyzerVitalsBlockData> Vitals = new();
}

/// <summary>
/// Raised when the health analyzer collects additional information to display in the abnormalities section
/// </summary>
[ByRefEvent]
public readonly record struct CollectHealthAnalyzerAbnormalitiesEvent()
{
    public readonly List<HealthAnalyzerAbnormalityData> Abnormalities = new();
}
