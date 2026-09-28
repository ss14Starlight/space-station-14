using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Medical.HealthAnalyzer;

[Serializable, NetSerializable]
public struct HealthAnalyzerVitalsBlockData
{
    public string Name;
    public string Value;

    public bool HasBar;
    public float BarRatio;

    public Color ValueColor;
    public Color BarColor;

    public SpriteSpecifier? Icon;
}

[Serializable, NetSerializable]
public struct HealthAnalyzerAbnormalityData
{
    public string Description;
    public Color Color;
}

[Serializable, NetSerializable]
public struct HealthAnalyzerExtensions
{
    public List<HealthAnalyzerVitalsBlockData> Vitals;
    public List<HealthAnalyzerAbnormalityData> Abnormalities;
}
