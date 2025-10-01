using Robust.Shared.Serialization;
using Robust.Shared.Analyzers;
using Robust.Shared.GameStates;

namespace Content.Shared.PlayerVendor;

[Serializable, NetSerializable]
public sealed class PlayerVendorUiState : BoundUserInterfaceState
{
    public readonly List<string> Entries;
    public readonly Dictionary<string, int> Amounts; 
        public readonly Dictionary<string, int> Prices;
    public readonly Dictionary<string, NetEntity?> Representatives;
    public readonly int DefaultPrice;
    public readonly int Balance;
    public readonly bool Locked;
    public readonly NetEntity? OwnerEntityNet;
    public readonly string? OwnerName;
    public readonly int ActiveDeposit;
    public readonly string? ActiveDepositorUserId;
    public readonly bool IsOwner; 
    public readonly bool IsActiveDepositor; 
    public readonly bool ShowBalance; 

    public PlayerVendorUiState(List<string> entries,
        Dictionary<string, int> amounts,
        Dictionary<string, int> prices,
        Dictionary<string, NetEntity?> representatives,
        int defaultPrice,
        int balance,
        bool locked,
        NetEntity? ownerEntityNet,
        string? ownerName,
        int activeDeposit,
        string? activeDepositorUserId,
        bool isOwner,
        bool isActiveDepositor,
        bool showBalance)
    {
        Entries = entries;
        Amounts = amounts;
        Prices = prices;
        Representatives = representatives;
        DefaultPrice = defaultPrice;
        Balance = balance;
        Locked = locked;
        OwnerEntityNet = ownerEntityNet;
        OwnerName = ownerName;
        ActiveDeposit = activeDeposit;
        ActiveDepositorUserId = activeDepositorUserId;
        IsOwner = isOwner;
        IsActiveDepositor = isActiveDepositor;
        ShowBalance = showBalance;
    }
}
