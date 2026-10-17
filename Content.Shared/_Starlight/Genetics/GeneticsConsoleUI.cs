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
public sealed class GeneticsConsoleDispenseSamplerMessage(NetEntity? uid = null) : BoundUserInterfaceMessage
{
    /// <summary>
    /// If specified, dispenses sampler in applicator mode instead.
    /// </summary>
    public readonly NetEntity? Uid = uid;
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleClearInputsMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class GeneticsConsoleSaveGeneMessage(HashSet<NetEntity> ancestors, TraitDict traits, string? name = null)
    : BoundUserInterfaceMessage
{
    public readonly HashSet<NetEntity> Ancestors = ancestors;
    public readonly TraitDict Traits = traits;
    public readonly string? Name = name;
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleUpdateStatusMessage(string? statusMessage) : BoundUserInterfaceMessage
{
    public readonly string? StatusMessage = statusMessage;
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleRefreshUiMessage : BoundUserInterfaceMessage;
