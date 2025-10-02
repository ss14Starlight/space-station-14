using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared.PlayerVendor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PlayerVendorComponent : Component
{
    [DataField]
    public string Container = "player_vendor_inventory";

    [DataField, AutoNetworkedField]
    public List<string> Entries = new();

    [DataField, AutoNetworkedField]
    public Dictionary<string, HashSet<NetEntity>> ContainedEntries = new();

    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Prices = new();

    [DataField, AutoNetworkedField]
    public int Balance;

    [DataField]
    public int DefaultPrice = 10;

    [DataField]
    public int MaxBalance = 50000;

    [DataField]
    public int MaxItemsPerEntry = 10;

    [DataField]
    public int MaxPricePerItem = 15000;

    [DataField, AutoNetworkedField]
    public bool Locked = true;

    [DataField, AutoNetworkedField]
    public NetEntity? OwnerEntity;

    [DataField, AutoNetworkedField]
    public string? OwnerName;

    [DataField, AutoNetworkedField]
    public string? CurrentDepositorUserId;

    [DataField, AutoNetworkedField]
    public int CurrentDepositAmount;

    [DataField]
    public SoundSpecifier? InsertSound = new SoundCollectionSpecifier("MachineInsert");

    [DataField]
    public SoundSpecifier VendSound = new SoundCollectionSpecifier("VendingDispense")
    {
        Params = new AudioParams
        {
            Volume = -4f,
            Variation = 0.15f
        }
    };

    [DataField]
    public SoundSpecifier DenySound = new SoundCollectionSpecifier("VendingDeny");

    [DataField, AutoNetworkedField]
    public bool Broken = false;
}

[Serializable, NetSerializable]
public enum PlayerVendorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum PlayerVendorVisuals : byte
{
    VisualState
}

[Serializable, NetSerializable]
public enum PlayerVendorVisualState : byte
{
    Normal,
    Off,
    Broken,
}

[Serializable, NetSerializable]
public enum PlayerVendorVisualLayers : byte
{
    Base,
    Panel,
}

[Serializable, NetSerializable]
public sealed class PlayerVendorPurchaseMessage(string entry) : BoundUserInterfaceMessage
{
    public string Entry = entry;
}

[Serializable, NetSerializable]
public sealed class PlayerVendorSetPriceMessage(string entry, int price) : BoundUserInterfaceMessage
{
    public string Entry = entry;
    public int Price = price;
}

[Serializable, NetSerializable]
public sealed class PlayerVendorWithdrawMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class PlayerVendorRefundDepositMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class PlayerVendorToggleLockMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class PlayerVendorClaimOwnershipMessage : BoundUserInterfaceMessage { }
