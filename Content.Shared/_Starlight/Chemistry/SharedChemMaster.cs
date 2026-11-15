using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

// ReSharper disable CheckNamespace
namespace Content.Shared.Chemistry;

/// <summary>
/// This class holds constants that are shared between client and server.
/// </summary>
public sealed partial class SharedChemMaster
{
    public const uint PillTypes = 20;
    public const string BufferSolutionName = "buffer";
    public const string InputSlotName = "beakerSlot";
    public const string OutputSlotName = "outputSlot";
    public const string PillSolutionName = "food";
    public const string PatchSolutionName = "patch"; //Starlight-edit
    public const string BottleSolutionName = "drink";
    public const uint LabelMaxLength = 50;
}

[Serializable, NetSerializable]
public sealed class ChemMasterSetModeMessage(ChemMasterMode mode) : BoundUserInterfaceMessage
{
    public readonly ChemMasterMode ChemMasterMode = mode;
}

[Serializable, NetSerializable]
public sealed class ChemMasterSetPillTypeMessage(uint pillType) : BoundUserInterfaceMessage
{
    public readonly uint PillType = pillType;
}

[Serializable, NetSerializable]
public sealed class ChemMasterReagentAmountButtonMessage(ReagentId reagentId, ChemMasterReagentAmount amount, bool fromBuffer) : BoundUserInterfaceMessage
{
    public readonly ReagentId ReagentId = reagentId;
    public readonly ChemMasterReagentAmount Amount = amount;
    public readonly bool FromBuffer = fromBuffer;
}

[Serializable, NetSerializable]
public sealed class ChemMasterCreatePillsMessage(uint dosage, uint number, string label, string containerLabel) : BoundUserInterfaceMessage
{
    public readonly uint Dosage = dosage;
    public readonly uint Number = number;
    public readonly string Label = label;
    public readonly string? ContainerLabel = containerLabel;
}

[Serializable, NetSerializable]
public sealed class ChemMasterOutputToBottleMessage(uint dosage, string label) : BoundUserInterfaceMessage
{
    public readonly uint Dosage = dosage;
    public readonly string Label = label;
}

[Serializable, NetSerializable]
public sealed class ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource drawSource) : BoundUserInterfaceMessage
{
    public readonly ChemMasterDrawSource DrawSource = drawSource;
}

public enum ChemMasterMode
{
    Transfer,
    Discard,
}

public enum ChemMasterSortingType : byte
{
    None = 0,
    Alphabetical = 1,
    Quantity = 2,
    Latest = 3,
}

[Serializable, NetSerializable]
public sealed class ChemMasterSortingTypeCycleMessage : BoundUserInterfaceMessage;

public enum ChemMasterReagentAmount
{
    U1 = 1,
    U5 = 5,
    U10 = 10,
    U15 = 15,
    U20 = 20,
    U30 = 30,
    U40 = 40,
    U60 = 60,
    U120 = 120,
    All,
}

public enum ChemMasterDrawSource
{
    Internal,
    External,
}

public static class ChemMasterReagentAmountToFixedPoint
{
    public static FixedPoint2 GetFixedPoint(this ChemMasterReagentAmount amount) => amount switch
    {
        ChemMasterReagentAmount.All => FixedPoint2.MaxValue,
        _ => FixedPoint2.New((int)amount)
    };
}

/// <summary>
/// Information about the capacity and contents of a container for display in the UI
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ContainerInfo(string displayName, FixedPoint2 currentVolume, FixedPoint2 maxVolume)
{
    /// <summary>
    /// The container name to show to the player
    /// </summary>
    public readonly string DisplayName = displayName;

    /// <summary>
    /// The currently used volume of the container
    /// </summary>
    public readonly FixedPoint2 CurrentVolume = currentVolume;

    /// <summary>
    /// The maximum volume of the container
    /// </summary>
    public readonly FixedPoint2 MaxVolume = maxVolume;

    /// <summary>
    /// A list of the pill entities and their sizes within the container
    /// STARLIGHT: Edited to only be pills and not both pills and patches.
    /// </summary>
    public List<(string Id, FixedPoint2 Quantity)>? PillEntities { get; init; }

    /// <summary>
    /// The label of the container, if one exists at all, to allow us to change the label of the container separate from the pills/patches.
    /// STARLIGHT:  Affects both pills and patches.
    /// </summary>
    public string? ContainerLabel { get; init;  }

    public NetEntity? Uid;

    public List<ReagentQuantity>? Reagents { get; init; }

    /// <summary>
    /// A list of the patch entities and their sizes within the container
    /// STARLIGHT: Added specifically for patches
    /// </summary>
    public List<(string Id, FixedPoint2 Quantity)>? PatchEntities { get; init; }

    public ContainerInfo DeepClone() => new(DisplayName, CurrentVolume, MaxVolume)
    {
        Uid = Uid,
        ContainerLabel = ContainerLabel,
        Reagents = Reagents == null ? null : new(Reagents),
        PatchEntities = PatchEntities == null ? null : new(PatchEntities),
        PillEntities = PillEntities == null ? null : new(PillEntities),
    };

    public static bool DeepEquals(ContainerInfo? a, ContainerInfo? b)
    {
        if (a == null || b == null)
            return a == null && b == null;

        if (a.DisplayName != b.DisplayName ||
            a.CurrentVolume != b.CurrentVolume ||
            a.MaxVolume != b.MaxVolume ||
            a.ContainerLabel != b.ContainerLabel ||
            a.Uid != b.Uid)
        {
            return false;
        }

        if (a.Reagents != null && b.Reagents != null)
        {
            if (a.Reagents.Count != b.Reagents.Count)
                return false;

            for (int i = 0; i < a.Reagents.Count; i++)
            {
                if (a.Reagents[i] != b.Reagents[i])
                    return false;
            }
        }
        else if (a.Reagents != null || b.Reagents != null)
            return false;

        static bool ListsEqual(List<(string Id, FixedPoint2 Quantity)>? a, List<(string Id, FixedPoint2 Quantity)>? b)
        {
            if (a != null && b != null)
            {
                if (a.Count != b.Count)
                    return false;

                for (int i = 0; i < a.Count; i++)
                {
                    if (a[i] != b[i])
                        return false;
                }
            }
            else if (a != null || b != null)
                return false;

            return true;
        }

        return ListsEqual(a.PillEntities, b.PillEntities) && ListsEqual(a.PatchEntities, b.PatchEntities);
    }
}

[Serializable, NetSerializable]
public enum ChemMasterUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class ChemMasterCreatePatchesMessage(uint dosage, uint number, string label, string containerLabel) : BoundUserInterfaceMessage
{
    public readonly uint Dosage = dosage;
    public readonly uint Number = number;
    public readonly string Label = label;
    public readonly string? ContainerLabel = containerLabel;
}

[Serializable, NetSerializable]
public sealed class ChemMasterToggleValveMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ChemMasterReagentCustomAmountButtonMessage(ReagentId reagentId, FixedPoint2 amount, bool fromBuffer) : BoundUserInterfaceMessage
{
    public readonly ReagentId ReagentId = reagentId;
    public readonly FixedPoint2 Amount = amount;
    public readonly bool FromBuffer = fromBuffer;
}

/// TRIESTE SPECIFIC
[Serializable, NetSerializable]
public sealed class ChemMasterSetTransferAmountMessage(ChemMasterReagentAmount amount) : BoundUserInterfaceMessage
{
    public ChemMasterReagentAmount Amount = amount;
}

