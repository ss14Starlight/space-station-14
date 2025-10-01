using Content.Shared.PlayerVendor;
using Robust.Client.Player;

namespace Content.Client.PlayerVendor;

public sealed class PlayerVendorBoundUserInterface : BoundUserInterface
{
    private PlayerVendorMenu? _menu;

    public PlayerVendorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _menu = new PlayerVendorMenu();
        _menu.OnPurchase += entry => SendPredictedMessage(new PlayerVendorPurchaseMessage(entry));
        _menu.OnSetPrice += (entry, price) => SendPredictedMessage(new PlayerVendorSetPriceMessage(entry, price));
        _menu.OnWithdraw += () => SendPredictedMessage(new PlayerVendorWithdrawMessage());
        _menu.OnToggleLock += () => SendPredictedMessage(new PlayerVendorToggleLockMessage());
        _menu.OnClaim += () => SendPredictedMessage(new PlayerVendorClaimOwnershipMessage());
        _menu.OnRefund += () => SendPredictedMessage(new PlayerVendorRefundDepositMessage());

        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var session = playerManager.LocalSession;
        _menu.SetCurrentUserId(session?.UserId.UserId.ToString());

        _menu.OpenCentered();
        Refresh();
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is PlayerVendorUiState ui)
            _menu?.Populate(ui);
        else
            Refresh();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _menu?.Close();
    }
    public void Refresh()
    {
        if (_menu == null)
            return;
        if (EntMan.TryGetComponent(Owner, out PlayerVendorComponent? comp))
        {
            var amounts = new Dictionary<string, int>();
            foreach (var e in comp.Entries)
            {
                if (comp.ContainedEntries.TryGetValue(e, out var set))
                    amounts[e] = set.Count;
                else
                    amounts[e] = 0;
            }
            var reps = new Dictionary<string, NetEntity?>();
            foreach (var e in comp.Entries)
            {
                if (comp.ContainedEntries.TryGetValue(e, out var set) && set.Count > 0)
                {
                    NetEntity? first = null;
                    foreach (var ent in set)
                    {
                        first = ent;
                        break;
                    }
                    reps[e] = first;
                }
                else
                {
                    reps[e] = null;
                }
            }
            var ui = new PlayerVendorUiState(
                new List<string>(comp.Entries),
                amounts,
                new Dictionary<string, int>(comp.Prices),
                reps,
                comp.DefaultPrice,
                comp.Balance,
                comp.Locked,
                comp.OwnerEntity,
                comp.OwnerName,
                comp.CurrentDepositAmount,
                comp.CurrentDepositorUserId,
                false,
                false, 
                true 
                );
            _menu.Populate(ui);
        }
    }
}
