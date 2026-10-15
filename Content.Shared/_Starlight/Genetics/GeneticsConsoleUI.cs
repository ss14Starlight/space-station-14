using Content.Shared._Starlight.Genetics.Genes.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics;

[Serializable, NetSerializable]
public enum GeneticsConsoleUiKey : byte
{
    Key
}

/// <summary>
/// Stages a gene, moving it from the gene pool to the input panel.
/// </summary>
[Serializable, NetSerializable]
public sealed class GeneticsConsoleStageGeneMessage(NetEntity uid) : BoundUserInterfaceMessage
{
    public readonly NetEntity Uid = uid;
}

/// <summary>
/// Unstages a gene, shifting it back to the gene pool from the input panel.
/// </summary>
[Serializable, NetSerializable]
public sealed class GeneticsConsoleUnstageGeneMessage(NetEntity uid) : BoundUserInterfaceMessage
{
    public readonly NetEntity Uid = uid;
}

/// <summary>
/// Set the sorting index for a panel on the console.
/// </summary>
[Serializable, NetSerializable]
public sealed class GeneticsConsoleSetSortIndex(int panel, int index) : BoundUserInterfaceMessage
{
    public readonly int Panel = panel;
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleRenameGeneMessage(NetEntity uid, string newName) : BoundUserInterfaceMessage
{
    public readonly NetEntity Uid = uid;
    public readonly string NewName = newName;
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleDeleteGeneMessage(NetEntity uid) : BoundUserInterfaceMessage
{
    public readonly NetEntity Uid = uid;
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleDispenseSamplerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class GeneticsConsoleClearInputsMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class GeneticsConsoleWriteToApplicatorMessage(TraitDict traits) : BoundUserInterfaceMessage
{
    public readonly TraitDict Traits = traits;
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

[Serializable, NetSerializable]
public sealed class GeneticsConsoleRefreshUiMessage : BoundUserInterfaceMessage;
