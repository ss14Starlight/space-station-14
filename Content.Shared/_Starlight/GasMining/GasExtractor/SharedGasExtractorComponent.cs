using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.GasMining.GasExtractor;

[Serializable, NetSerializable]
public enum GasExtractorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GasExtractorToggleStatusMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; }

    public GasExtractorToggleStatusMessage(bool enabled) => Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class GasExtractorChangeExtractionRateMessage : BoundUserInterfaceMessage
{
    public float ExtractionRate { get; }

    public GasExtractorChangeExtractionRateMessage(float rate) => ExtractionRate = rate;
}
