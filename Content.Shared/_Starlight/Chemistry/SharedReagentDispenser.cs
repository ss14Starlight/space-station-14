using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Chemistry;

/// <summary>
/// This class holds constants that are shared between client and server.
/// </summary>
public sealed class SharedReagentDispenser
{
    public const string OutputSlotName = "beakerSlot";
}

[Serializable, NetSerializable]
public sealed class ReagentDispenserSetDispenseAmountMessage : BoundUserInterfaceMessage
{
    public readonly ReagentDispenserDispenseAmount ReagentDispenserDispenseAmount;

    public ReagentDispenserSetDispenseAmountMessage(ReagentDispenserDispenseAmount amount) =>
        ReagentDispenserDispenseAmount = amount;

    /// <summary>
    ///     Create a new instance from interpreting a String as an integer,
    ///     throwing an exception if it is unable to parse.
    /// </summary>
    public ReagentDispenserSetDispenseAmountMessage(string s) => ReagentDispenserDispenseAmount = s switch
    {
        "1" => ReagentDispenserDispenseAmount.U1,
        "5" => ReagentDispenserDispenseAmount.U5,
        "10" => ReagentDispenserDispenseAmount.U10,
        "15" => ReagentDispenserDispenseAmount.U15,
        "20" => ReagentDispenserDispenseAmount.U20,
        "30" => ReagentDispenserDispenseAmount.U30,
        "40" => ReagentDispenserDispenseAmount.U40,
        "60" => ReagentDispenserDispenseAmount.U60,
        "120" => ReagentDispenserDispenseAmount.U120,
        _ => throw new Exception($"Cannot convert the string `{s}` into a valid ReagentDispenser DispenseAmount"),
    };
}

[Serializable, NetSerializable]
public sealed class ReagentDispenserDispenseReagentMessage(ReagentDispenseData data) : BoundUserInterfaceMessage
{
    public readonly ReagentDispenseData Data = data;
}

/// <summary>
///     Message sent by the user interface to ask the reagent dispenser to eject a container
/// </summary>
[Serializable, NetSerializable]
public sealed class ReagentDispenserEjectContainerMessage(ItemStorageLocation storageLocation) : BoundUserInterfaceMessage
{
    public readonly ItemStorageLocation StorageLocation = storageLocation;
}

[Serializable, NetSerializable]
public sealed class ReagentDispenserClearContainerSolutionMessage : BoundUserInterfaceMessage
{
}

/// <summary>
///     Message sent by the user interface to toggle the plumbing valve.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReagentDispenserToggleValveMessage : BoundUserInterfaceMessage
{
}

public enum ReagentDispenserDispenseAmount
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
}

[Serializable, NetSerializable]
public sealed record class ReagentInventoryItem(ReagentDispenseData Data, string ReagentLabel, FixedPoint2 Quantity, Color ReagentColor, bool Generatable)
{
}

[Serializable, NetSerializable]
public sealed record class ReagentDispenseData(ItemStorageLocation? StorageLocation, ProtoId<ReagentPrototype>? ReagentID)
{
}

[Serializable, NetSerializable]
public enum ReagentDispenserUiKey
{
    Key
}
