using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology;

public static class SharedCultureIncubator
{
    public const string ChamberContainerId = "chamber";
}

[Serializable, NetSerializable]
public enum CultureIncubatorUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CultureIncubatorBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Powered;
    public bool Busy;
    public bool HasFinished;
    public float NutrientAmount;
    public float NutrientMax;
    public string NutrientReagent;
    public int MaxSamples;
    public float EstimatedNutrientCost;
    public CultureIncubatorSampleState[] Samples;
    public TimeSpan CycleStartedAt;
    public TimeSpan CycleEndsAt;

    public CultureIncubatorBoundUserInterfaceState(
        bool powered,
        bool busy,
        bool hasFinished,
        float nutrientAmount,
        float nutrientMax,
        string nutrientReagent,
        int maxSamples,
        float estimatedNutrientCost,
        CultureIncubatorSampleState[] samples,
        TimeSpan cycleStartedAt,
        TimeSpan cycleEndsAt)
    {
        Powered = powered;
        Busy = busy;
        HasFinished = hasFinished;
        NutrientAmount = nutrientAmount;
        NutrientMax = nutrientMax;
        NutrientReagent = nutrientReagent;
        MaxSamples = maxSamples;
        EstimatedNutrientCost = estimatedNutrientCost;
        Samples = samples;
        CycleStartedAt = cycleStartedAt;
        CycleEndsAt = cycleEndsAt;
    }
}

[Serializable, NetSerializable]
public struct CultureIncubatorSampleState
{
    public NetEntity Entity;
    public string Label;
    public float Quality;
    public bool Contaminated;

    /// <summary>
    /// Extra detail line (detected genetics for samples, culture type for finished vials).
    /// </summary>
    public string Detail;

    public CultureIncubatorSampleState(NetEntity entity, string label, float quality, bool contaminated, string detail)
    {
        Entity = entity;
        Label = label;
        Quality = quality;
        Contaminated = contaminated;
        Detail = detail;
    }
}

[Serializable, NetSerializable]
public sealed class CultureIncubatorStartMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CultureIncubatorEjectSampleMessage : BoundUserInterfaceMessage
{
    public NetEntity Sample;

    public CultureIncubatorEjectSampleMessage(NetEntity sample)
    {
        Sample = sample;
    }
}

[Serializable, NetSerializable]
public sealed class CultureIncubatorEjectAllMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CultureIncubatorRetrieveMessage : BoundUserInterfaceMessage;
