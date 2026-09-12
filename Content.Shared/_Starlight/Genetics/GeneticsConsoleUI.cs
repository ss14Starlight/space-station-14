using Content.Shared._Starlight.Genetics.Genes.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics;

[Serializable, NetSerializable]
public enum GeneticsConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleState : BoundUserInterfaceState
{
    public List<GeneData> Genes;

    public GeneticsConsoleState(List<GeneData> genes)
    {
        Genes = genes;
    }
}

/// <summary>
/// The data required to render the console UI for a single gene.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct GeneData
{
    [DataField]
    public TraitDict Traits;

    [DataField]
    public string TechnicalName;

    [DataField]
    public string? Name;

    public GeneData(IndividualGeneComponent individual)
    {
        Traits = new TraitDict(individual.Traits.Traits);
        TechnicalName = individual.TechnicalName;
        Name = individual.Name;
    }
}

[DataRecord, Serializable, NetSerializable]
public partial record struct GeneticsConsoleRenameGeneData // Is this redundant? I'm genuinely unsure.
{
    public readonly int Index;
    public readonly string NewName;

    public GeneticsConsoleRenameGeneData(int index, string newName)
    {
        Index = index;
        NewName = newName;
    }
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleRenameGeneMessage : BoundUserInterfaceMessage
{
    public readonly int Index;
    public readonly string NewName;

    public GeneticsConsoleRenameGeneMessage(int index, string newName)
    {
        Index = index;
        NewName = newName;
    }

    public GeneticsConsoleRenameGeneMessage(GeneticsConsoleRenameGeneData data)
    {
        Index = data.Index;
        NewName = data.NewName;
    }
}
