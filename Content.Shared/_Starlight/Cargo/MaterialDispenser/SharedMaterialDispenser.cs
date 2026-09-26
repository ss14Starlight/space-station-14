using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.MaterialDispenser;

[Serializable, NetSerializable]
public enum MaterialDispenserUiKey
{
    Key
}

[Serializable, NetSerializable]
public enum MaterialDispenserMode
{
    Transfer,
    Eject
}

[Serializable, NetSerializable]
public sealed class MaterialDispenserBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly MaterialDispenserMode Mode;
    public readonly string CargoAccount;
    public readonly Dictionary<string, int> Buffer;

    public MaterialDispenserBoundUserInterfaceState(MaterialDispenserMode mode, string cargoAccount, Dictionary<string, int> buffer)
    {
        Mode = mode;
        CargoAccount = cargoAccount;
        Buffer = buffer;
    }
}

[Serializable, NetSerializable]
public sealed class MaterialDispenserDepartmentSelected : BoundUserInterfaceMessage
{
    public readonly string Department;

    public MaterialDispenserDepartmentSelected(string department) => Department = department;
}

[Serializable, NetSerializable]
public sealed class MaterialDispenserEjectCrate : BoundUserInterfaceMessage
{

}

[Serializable, NetSerializable]
public sealed class MaterialDispenserModeChange : BoundUserInterfaceMessage
{
    public readonly MaterialDispenserMode Mode;
    public MaterialDispenserModeChange(MaterialDispenserMode mode) => Mode = mode;
}

[Serializable, NetSerializable]
public sealed class MaterialDispenserAmountButton : BoundUserInterfaceMessage
{
    public readonly string Material;
    public readonly int Amount;
    public readonly bool FromBuffer;

    public MaterialDispenserAmountButton(string material, int amount, bool fromBuffer)
    {
        Material = material;
        Amount = amount;
        FromBuffer = fromBuffer;
    }
}
