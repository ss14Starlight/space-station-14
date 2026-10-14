using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes; // Starlight-edit

namespace Content.Shared.Chemistry
{
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

        public ReagentDispenserSetDispenseAmountMessage(ReagentDispenserDispenseAmount amount)
        {
            ReagentDispenserDispenseAmount = amount;
        }

        /// <summary>
        ///     Create a new instance from interpreting a String as an integer,
        ///     throwing an exception if it is unable to parse.
        /// </summary>
        public ReagentDispenserSetDispenseAmountMessage(String s)
        {
            switch (s)
            {
                case "1":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U1;
                    break;
                case "5":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U5;
                    break;
                case "10":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U10;
                    break;
                case "15":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U15;
                    break;
                case "20":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U20;
                    break;
                case "30":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U30;
                    break;
                case "40":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U40;
                    break;
                case "60":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U60;
                    break;
                case "120":
                    ReagentDispenserDispenseAmount = ReagentDispenserDispenseAmount.U120;
                    break;
                default:
                    throw new Exception($"Cannot convert the string `{s}` into a valid ReagentDispenser DispenseAmount");
            }
        }
    }

    [Serializable, NetSerializable]
    public sealed class ReagentDispenserDispenseReagentMessage : BoundUserInterfaceMessage
    {
        public readonly ReagentDispenseData Data; // Starlight-edit

        public ReagentDispenserDispenseReagentMessage(ReagentDispenseData data) // Starlight-edit
        {
            Data = data; // Starlight-edit
        }
    }

    /// <summary>
    ///     Message sent by the user interface to ask the reagent dispenser to eject a container
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class ReagentDispenserEjectContainerMessage : BoundUserInterfaceMessage
    {
        public readonly ItemStorageLocation StorageLocation;

        public ReagentDispenserEjectContainerMessage(ItemStorageLocation storageLocation)
        {
            StorageLocation = storageLocation;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ReagentDispenserClearContainerSolutionMessage : BoundUserInterfaceMessage
    {

    }

    // Starlight-start: Plumbing valve toggle
    /// <summary>
    ///     Message sent by the user interface to toggle the plumbing valve.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class ReagentDispenserToggleValveMessage : BoundUserInterfaceMessage
    {
    }
    // Starlight-end

    // Starlight Start
    // Required for UI to not flash while cell is charging/discharging
    [Serializable, NetSerializable]
    public sealed class ReagentDispenserEnergyUpdateMessage : BoundUserInterfaceMessage
    {
        public readonly float EnergyAmount;

        public ReagentDispenserEnergyUpdateMessage(float energyAmount)
        {
            EnergyAmount = energyAmount;
        }
    }
    // Starlight End

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
    public sealed record class ReagentInventoryItem(ReagentDispenseData Data, string ReagentLabel, FixedPoint2 Quantity, Color ReagentColor, bool Generatable) // Starlight-edit
    {
    }

    // Starlight-start: Generatable reagents
    [Serializable, NetSerializable]
    public sealed record class ReagentDispenseData(ItemStorageLocation? StorageLocation, ProtoId<ReagentPrototype>? ReagentID)
    {
    }
    // Starlight-end

    [Serializable, NetSerializable]
    public enum ReagentDispenserUiKey
    {
        Key
    }
}
