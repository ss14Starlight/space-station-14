using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology;

public static class SharedPathogenSynthesizer
{
    public const string SubstrateSlotId = "substrateSlot";
    public const string GeneStorageContainerId = "geneStorage";
}

[Serializable, NetSerializable]
public enum PathogenSynthesizerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class PathogenSynthesizerBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Powered;
    public bool Busy;
    public float StabilizerAmount;
    public float StabilizerMax;
    public string StabilizerReagent;
    public float StabilizerNeeded;
    public int BudgetUsed;
    public int MaxBudget;
    public float EstimatedSeconds;
    public bool HasSubstrate;
    public string? SubstrateName;
    public PathogenSynthesizerGeneState[] Genes;
    public string? ValidationError;
    public bool CanStart;
    public TimeSpan CycleStartedAt;
    public TimeSpan CycleEndsAt;
    public PathogenSynthesizerForecastState? Forecast;

    public PathogenSynthesizerBoundUserInterfaceState(
        bool powered,
        bool busy,
        float stabilizerAmount,
        float stabilizerMax,
        string stabilizerReagent,
        float stabilizerNeeded,
        int budgetUsed,
        int maxBudget,
        float estimatedSeconds,
        bool hasSubstrate,
        string? substrateName,
        PathogenSynthesizerGeneState[] genes,
        string? validationError,
        bool canStart,
        TimeSpan cycleStartedAt,
        TimeSpan cycleEndsAt,
        PathogenSynthesizerForecastState? forecast)
    {
        Powered = powered;
        Busy = busy;
        StabilizerAmount = stabilizerAmount;
        StabilizerMax = stabilizerMax;
        StabilizerReagent = stabilizerReagent;
        StabilizerNeeded = stabilizerNeeded;
        BudgetUsed = budgetUsed;
        MaxBudget = maxBudget;
        EstimatedSeconds = estimatedSeconds;
        HasSubstrate = hasSubstrate;
        SubstrateName = substrateName;
        Genes = genes;
        ValidationError = validationError;
        CanStart = canStart;
        CycleStartedAt = cycleStartedAt;
        CycleEndsAt = cycleEndsAt;
        Forecast = forecast;
    }
}

[Serializable, NetSerializable]
public struct PathogenSynthesizerGeneState
{
    public NetEntity Entity;
    public string Label;
    public int BudgetCost;
    public bool Selected;

    public PathogenSynthesizerGeneState(NetEntity entity, string label, int budgetCost, bool selected)
    {
        Entity = entity;
        Label = label;
        BudgetCost = budgetCost;
        Selected = selected;
    }
}

/// <summary>
/// Player-facing forecast of the strain assembled from the current gene selection.
/// </summary>
[Serializable, NetSerializable]
public sealed class PathogenSynthesizerForecastState
{
    public string Transmission;
    public string Incubation;
    public string Symptomatic;
    public string Critical;
    public string Recovery;
    public string Symptoms;
    public string Organs;
    public string Treatments;
    public string Infectivity;
    public string Lethality;
    public string Sterilant;

    public PathogenSynthesizerForecastState(
        string transmission,
        string incubation,
        string symptomatic,
        string critical,
        string recovery,
        string symptoms,
        string organs,
        string treatments,
        string infectivity,
        string lethality,
        string sterilant)
    {
        Transmission = transmission;
        Incubation = incubation;
        Symptomatic = symptomatic;
        Critical = critical;
        Recovery = recovery;
        Symptoms = symptoms;
        Organs = organs;
        Treatments = treatments;
        Infectivity = infectivity;
        Lethality = lethality;
        Sterilant = sterilant;
    }
}

[Serializable, NetSerializable]
public sealed class PathogenSynthesizerStartMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PathogenSynthesizerToggleGeneMessage : BoundUserInterfaceMessage
{
    public NetEntity Gene;

    public PathogenSynthesizerToggleGeneMessage(NetEntity gene)
    {
        Gene = gene;
    }
}

[Serializable, NetSerializable]
public sealed class PathogenSynthesizerClearSelectionMessage : BoundUserInterfaceMessage;
